import { Body, Controller, Get, Put, UseGuards } from "@nestjs/common";
import { CurrentUser } from "../../common/decorators/current-user.decorator";
import { JwtAuthGuard } from "../../common/guards/jwt-auth.guard";
import { UsersService } from "./users.service";

@Controller("auth/profile")
@UseGuards(JwtAuthGuard)
export class UsersController {
  constructor(private readonly usersService: UsersService) {}

  @Get()
  async getProfile(@CurrentUser("id") userId: string) {
    return this.usersService.getProfile(userId);
  }

  @Put()
  async updateProfile(@CurrentUser("id") userId: string, @Body() body: any) {
    return this.usersService.updateProfile(userId, body);
  }
}
