import { Injectable, NotFoundException } from "@nestjs/common";
import { InjectModel } from "@nestjs/mongoose";
import { Model } from "mongoose";
import { Notification, NotificationDocument } from "../../database/schemas";

@Injectable()
export class NotificationsService {
  constructor(
    @InjectModel(Notification.name)
    private notificationModel: Model<NotificationDocument>
  ) {}

  async getNotifications(userId: string, unreadOnly: boolean) {
    const query: any = { recipient: userId };
    if (unreadOnly) query.read = false;

    const notifications = await this.notificationModel
      .find(query)
      .populate("order", "orderNumber status")
      .sort({ createdAt: -1 })
      .limit(50);

    return { notifications };
  }

  async markAsRead(notificationId: string, userId: string) {
    const notification = await this.notificationModel.findOneAndUpdate(
      { _id: notificationId, recipient: userId },
      { read: true },
      { new: true }
    );

    if (!notification) throw new NotFoundException("Notification not found");
    return { notification };
  }
}
