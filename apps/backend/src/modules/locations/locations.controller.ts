import { Controller, Get, Param } from "@nestjs/common";
import { LocationsService } from "./locations.service";

@Controller("locations")
export class LocationsController {
  constructor(private readonly locationsService: LocationsService) {}

  @Get()
  async getLocations() {
    return this.locationsService.getActiveLocations();
  }

  @Get(":id/availability")
  async getAvailability(@Param("id") id: string) {
    return this.locationsService.getAvailability(id);
  }
}
