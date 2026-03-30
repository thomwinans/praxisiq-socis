namespace Snapp.Shared.DTOs.Transaction;

public class DealRoomListResponse
{
    public List<DealRoomResponse> DealRooms { get; set; } = [];

    public string? NextToken { get; set; }
}
