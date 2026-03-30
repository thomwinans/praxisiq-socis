using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.Transaction;

public class CreateDealRoomRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
}
