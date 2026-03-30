using System.ComponentModel.DataAnnotations;
using Snapp.Shared.Enums;

namespace Snapp.Shared.DTOs.Transaction;

public class UpdateReferralStatusRequest
{
    [Required]
    public ReferralStatus Status { get; set; }
}
