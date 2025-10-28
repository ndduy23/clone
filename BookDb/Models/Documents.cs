using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace BookDb.Models
{
    public class Document
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(500)]
        public string Title { get; set; }         

        [MaxLength(200)]
        public string Category { get; set; }      

        [MaxLength(200)]
        public string Author { get; set; }         

        [Required, MaxLength(500)]
        public string FileName { get; set; }       

        public long FileSize { get; set; }          

        public string? FilePath { get; set; }   

        [MaxLength(100)]
        public string? ContentType { get; set; }   

        public string? Description { get; set; }

        public bool IsPublic { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<DocumentPage> Pages { get; set; }
    }

}
