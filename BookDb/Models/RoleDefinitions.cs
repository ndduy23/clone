namespace BookDb.Models
{
    /// <summary>
    /// Định nghĩa các role trong hệ thống
    /// </summary>
    public static class Roles
    {
        /// <summary>
        /// Quản trị viên - Toàn quyền trên hệ thống
        /// </summary>
        public const string Admin = "Admin";

        /// <summary>
        /// Người quản lý - Quản lý tài liệu và người dùng
        /// </summary>
        public const string Manager = "Manager";

        /// <summary>
        /// Biên tập viên - Tạo, sửa, xóa tài liệu
        /// </summary>
        public const string Editor = "Editor";

        /// <summary>
        /// Người đóng góp - Chỉ tạo và sửa tài liệu của mình
        /// </summary>
        public const string Contributor = "Contributor";

        /// <summary>
        /// Người dùng thông thường - Chỉ xem và bookmark
        /// </summary>
        public const string User = "User";

        /// <summary>
        /// Khách - Chỉ xem tài liệu công khai
        /// </summary>
        public const string Guest = "Guest";

        /// <summary>
        /// Lấy tất cả các role
        /// </summary>
        public static string[] GetAllRoles()
        {
            return new[] { Admin, Manager, Editor, Contributor, User, Guest };
        }

        /// <summary>
        /// Lấy mô tả chi tiết của role
        /// </summary>
        public static Dictionary<string, RoleDescription> GetRoleDescriptions()
        {
            return new Dictionary<string, RoleDescription>
            {
                {
                    Admin,
                    new RoleDescription
                    {
                        Name = "Quản trị viên",
                        Description = "Toàn quyền quản lý hệ thống",
                        Permissions = new[]
                        {
                            "Quản lý người dùng và phân quyền",
                            "Quản lý tất cả tài liệu",
                            "Xem báo cáo và thống kê",
                            "Cấu hình hệ thống",
                            "Xóa bất kỳ tài liệu nào",
                            "Khóa/Mở khóa tài khoản"
                        }
                    }
                },
                {
                    Manager,
                    new RoleDescription
                    {
                        Name = "Người quản lý",
                        Description = "Quản lý tài liệu và người dùng trong phạm vi được giao",
                        Permissions = new[]
                        {
                            "Quản lý người dùng (không bao gồm Admin)",
                            "Phê duyệt tài liệu",
                            "Xem báo cáo",
                            "Quản lý tài liệu trong danh mục được giao",
                            "Gán quyền Editor/Contributor"
                        }
                    }
                },
                {
                    Editor,
                    new RoleDescription
                    {
                        Name = "Biên tập viên",
                        Description = "Tạo, chỉnh sửa và xóa tài liệu",
                        Permissions = new[]
                        {
                            "Tạo tài liệu mới",
                            "Chỉnh sửa tất cả tài liệu",
                            "Xóa tài liệu (trừ tài liệu của Admin)",
                            "Quản lý bookmark của mình",
                            "Xem tất cả tài liệu"
                        }
                    }
                },
                {
                    Contributor,
                    new RoleDescription
                    {
                        Name = "Người đóng góp",
                        Description = "Tạo và chỉnh sửa tài liệu của riêng mình",
                        Permissions = new[]
                        {
                            "Tạo tài liệu mới",
                            "Chỉnh sửa tài liệu của mình",
                            "Xóa tài liệu của mình",
                            "Quản lý bookmark của mình",
                            "Xem tất cả tài liệu công khai"
                        }
                    }
                },
                {
                    User,
                    new RoleDescription
                    {
                        Name = "Người dùng",
                        Description = "Xem và tạo bookmark cho tài liệu",
                        Permissions = new[]
                        {
                            "Xem tất cả tài liệu công khai",
                            "Tạo bookmark",
                            "Quản lý bookmark của mình",
                            "Tìm kiếm tài liệu",
                            "Tải xuống tài liệu"
                        }
                    }
                },
                {
                    Guest,
                    new RoleDescription
                    {
                        Name = "Khách",
                        Description = "Chỉ xem tài liệu công khai",
                        Permissions = new[]
                        {
                            "Xem tài liệu công khai",
                            "Tìm kiếm tài liệu công khai"
                        }
                    }
                }
            };
        }
    }

    /// <summary>
    /// Mô tả chi tiết của role
    /// </summary>
    public class RoleDescription
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string[] Permissions { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Policy names cho authorization
    /// </summary>
    public static class Policies
    {
        public const string RequireAdminRole = "RequireAdminRole";
        public const string RequireManagerRole = "RequireManagerRole";
        public const string RequireEditorRole = "RequireEditorRole";
        public const string RequireContributorRole = "RequireContributorRole";
        public const string CanManageDocuments = "CanManageDocuments";
        public const string CanEditDocuments = "CanEditDocuments";
        public const string CanViewDocuments = "CanViewDocuments";
        public const string CanManageUsers = "CanManageUsers";
    }
}