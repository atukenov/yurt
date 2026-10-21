namespace Yurt.Application.Features.Stations;

public record StationDto(Guid Id, string Name, int Categories);
public record SaveStationDto(string Name);
public record SetCategoryStationDto(Guid? StationId);

public record CategoryRoutingDto(Guid CategoryId, string CategoryName, Guid? StationId);

/// <summary>Everything the kitchen screen needs to split orders by station: the stations and the station of every menu item.</summary>
public record RoutingDto(List<StationDto> Stations, List<CategoryRoutingDto> Categories, Dictionary<Guid, Guid> ItemStations);
