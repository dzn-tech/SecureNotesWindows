using System;
using System.Collections.Generic;

namespace SecureNotesWin.Models
{
    public enum NotebookType { NOTEBOOK, DIARY }

    public class Notebook
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public string? ParentId { get; set; }
        public string Icon { get; set; } = "📓";
        public int SortOrder { get; set; } = 0;
        public NotebookType Type { get; set; } = NotebookType.NOTEBOOK;
    }

    public class Note
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? NotebookId { get; set; }
        public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public bool IsDiary { get; set; } = false;
        public bool IsFavorite { get; set; } = false;
        public bool IsTodo { get; set; } = false;
        public bool IsDone { get; set; } = false;
        public List<string> AttachmentNames { get; set; } = new();
        public Dictionary<string, byte[]> Attachments { get; set; } = new();

        public Note Clone() => new Note
        {
            Id = Id, Title = Title, Body = Body, NotebookId = NotebookId,
            CreatedAt = CreatedAt, UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            IsDiary = IsDiary, IsFavorite = IsFavorite, IsTodo = IsTodo, IsDone = IsDone,
            AttachmentNames = new List<string>(AttachmentNames),
            Attachments = new Dictionary<string, byte[]>(Attachments)
        };
    }

    public class Tag
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public string Color { get; set; } = "#FFB347";
    }

    // --- JSON serialization helpers (mirrors Android app's internal JSON DTOs) ---

    public class NotebookJson
    {
        public string id { get; set; } = string.Empty;
        public string title { get; set; } = string.Empty;
        public long createdAt { get; set; }
        public long updatedAt { get; set; }
        public string? parentId { get; set; }
        public string icon { get; set; } = "📓";
        public int sortOrder { get; set; }
        public string? type { get; set; }

        public Notebook ToNotebook() => new Notebook
        {
            Id = id, Title = title, CreatedAt = createdAt, UpdatedAt = updatedAt,
            ParentId = parentId, Icon = icon, SortOrder = sortOrder,
            Type = type != null && Enum.TryParse<NotebookType>(type, out var t) ? t : NotebookType.NOTEBOOK
        };

        public static NotebookJson From(Notebook nb) => new NotebookJson
        {
            id = nb.Id, title = nb.Title, createdAt = nb.CreatedAt, updatedAt = nb.UpdatedAt,
            parentId = nb.ParentId, icon = nb.Icon, sortOrder = nb.SortOrder, type = nb.Type.ToString()
        };
    }

    public class TagJson
    {
        public string id { get; set; } = string.Empty;
        public string title { get; set; } = string.Empty;
        public long createdAt { get; set; }
        public string color { get; set; } = "#FFB347";

        public Tag ToTag() => new Tag { Id = id, Title = title, CreatedAt = createdAt, Color = color };
        public static TagJson From(Tag tag) => new TagJson { id = tag.Id, title = tag.Title, createdAt = tag.CreatedAt, color = tag.Color };
    }
}
