import { ConflictException, Injectable } from "@nestjs/common";
import { InjectModel } from "@nestjs/mongoose";
import { Model } from "mongoose";
import { User, UserDocument } from "../../database/schemas";

@Injectable()
export class AuthService {
  constructor(@InjectModel(User.name) private userModel: Model<UserDocument>) {}

  async register(data: {
    email: string;
    password: string;
    name: string;
    phone?: string;
  }) {
    const { email, password, name, phone } = data;

    const existingUser = await this.userModel.findOne({ email });
    if (existingUser) {
      throw new ConflictException("User already exists");
    }

    const user = await this.userModel.create({
      email,
      pinCode: password,
      name,
      phone,
      role: "customer",
    });

    return {
      message: "User registered successfully",
      user: {
        id: user._id,
        email: user.email,
        name: user.name,
      },
    };
  }
}
