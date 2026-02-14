import {
  BadRequestException,
  Body,
  Controller,
  HttpCode,
  HttpStatus,
  Post,
} from "@nestjs/common";
import { z } from "zod";
import { AuthService } from "./auth.service";

const registerSchema = z.object({
  email: z.string().email(),
  password: z
    .string()
    .length(4, "PIN must be exactly 4 digits")
    .regex(/^\d+$/, "PIN must contain only digits"),
  name: z.string().min(1),
  phone: z.string().optional(),
});

@Controller("auth")
export class AuthController {
  constructor(private readonly authService: AuthService) {}

  @Post("register")
  @HttpCode(HttpStatus.CREATED)
  async register(@Body() body: any) {
    const validation = registerSchema.safeParse(body);
    if (!validation.success) {
      throw new BadRequestException("Invalid input");
    }
    return this.authService.register(validation.data);
  }
}
