namespace Snapp.Shared.Auth;

[Flags]
public enum Permission
{
    None = 0,
    ViewMembers = 1,
    ManageMembers = 2,
    CreatePost = 4,
    ModerateContent = 8,
    ManageNetwork = 16,
    ManageRoles = 32,
    ReviewApplications = 64,
    ViewIntelligence = 128,
    ManageReferrals = 256,
    ManageDealRooms = 512,
    Admin = int.MaxValue
}
