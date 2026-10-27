using System;

namespace SeOne.Domain.Entities;

public sealed class ClassroomMaterial
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "course_material";
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string ObjectPath { get; set; } = string.Empty;
    public Guid UploadedById { get; set; }
    public DateTime CreatedAt { get; set; }

    public ClassroomSession Session { get; set; } = null!;
    public User UploadedBy { get; set; } = null!;
}
