import {
  CanActivate,
  ExecutionContext,
  Injectable,
  Logger,
  UnauthorizedException,
} from "@nestjs/common";
import { ConfigService } from "@nestjs/config";
import { jwtDecrypt } from "jose";
import { hkdf } from "node:crypto";
import { promisify } from "node:util";

const hkdfAsync = promisify(hkdf);

@Injectable()
export class JwtAuthGuard implements CanActivate {
  private readonly logger = new Logger(JwtAuthGuard.name);

  constructor(private readonly configService: ConfigService) {}

  async canActivate(context: ExecutionContext): Promise<boolean> {
    const request = context.switchToHttp().getRequest();
    const token = this.extractToken(request);

    if (!token) {
      throw new UnauthorizedException("No authentication token provided");
    }

    try {
      const secret = this.configService.get<string>("NEXTAUTH_SECRET");
      if (!secret) {
        throw new Error("NEXTAUTH_SECRET not configured");
      }

      // Derive the encryption key the same way NextAuth does
      // NextAuth uses HKDF with SHA-256 to derive a 32-byte encryption key
      const derivedKey = await hkdfAsync(
        "sha256",
        secret,
        "",
        "NextAuth.js Generated Encryption Key",
        32
      );
      const encryptionKey = new Uint8Array(derivedKey);

      // Decrypt the JWE token
      const { payload } = await jwtDecrypt(token, encryptionKey, {
        clockTolerance: 15,
      });

      // Attach user to request (same shape as NextAuth session.user)
      request.user = {
        id: payload.id || payload.sub,
        role: payload.role,
        email: payload.email,
        name: payload.name,
      };

      return true;
    } catch (error) {
      this.logger.warn(`JWT validation failed: ${(error as Error).message}`);
      throw new UnauthorizedException("Invalid or expired token");
    }
  }

  private extractToken(request: any): string | null {
    // Try Authorization: Bearer <token> header first
    const authHeader = request.headers?.authorization;
    if (authHeader?.startsWith("Bearer ")) {
      return authHeader.substring(7);
    }

    // Fallback: try to read the NextAuth session cookie
    const sessionToken =
      request.cookies?.["next-auth.session-token"] ||
      request.cookies?.["__Secure-next-auth.session-token"];
    if (sessionToken) {
      return sessionToken;
    }

    return null;
  }
}
