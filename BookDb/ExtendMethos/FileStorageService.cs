namespace BookDb.ExtendMethos
{
    public class FileStorageService
    {
        private readonly IWebHostEnvironment _env;
        public FileStorageService(IWebHostEnvironment env) => _env = env;

        public string GetDocumentFolder(Guid documentId)
        {
            var basePath = Path.Combine(_env.WebRootPath, "uploads", documentId.ToString());
            if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);
            return basePath;
        }

        public async Task<(string filePath, long size)> SavePageFileAsync(Guid documentId, int pageNumber, IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName);
            var folder = GetDocumentFolder(documentId);
            var fileName = $"{pageNumber}{ext}";
            var fullPath = Path.Combine(folder, fileName);
            using (var stream = File.Create(fullPath))
            {
                await file.CopyToAsync(stream);
            }
            var relPath = $"/uploads/{documentId}/{fileName}";
            var size = new FileInfo(fullPath).Length;
            return (relPath, size);
        }
    }
}
