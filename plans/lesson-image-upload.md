# Plan: Upload ảnh trong bài học (Đã hoàn thành)

## Mục tiêu

Cho phép giáo viên chèn ảnh vào nội dung bài học. Ảnh được lưu riêng tư trong file/blob storage; database lưu metadata và nội dung bài học giữ tham chiếu đến ảnh. Backend kiểm tra quyền mỗi lần upload hoặc xem ảnh.

## Thiết kế đã chốt và triển khai

- **Cơ chế lưu trữ:** FileSystem Storage lưu file vào thư mục riêng tư ngoài `wwwroot` (`App_Data/LessonImages`), database chỉ lưu metadata (`AppLessonImages`).
- **Giới hạn file:**
  - Định dạng hỗ trợ: JPEG, JPG, PNG, WebP, GIF.
  - Dung lượng tối đa: 5 MB/ảnh.
  - Kiểm tra binary signatures (magic bytes) ở server để phòng chống giả mạo extension.
- **Entity metadata `LessonImage`:**
  - `Id` (Guid), `LessonId` (long, nullable cho quá trình tạo bài mới), `ChapterId` (long, nullable), `BlobName` (Guid-based an toàn), `OriginalFileName`, `ContentType`, `FileSize`.
- **API Endpoints (`/api/lesson-images`):**
  - `POST /api/lesson-images/upload`: Nhận multipart file cùng `chapterId` / `lessonId`. Kiểm tra quyền giảng viên của môn học hoặc Admin. Lưu file vào storage và tạo metadata, trả về `{ id, url, fileName, fileSize, contentType }`.
  - `GET /api/lesson-images/{id}`: Kiểm tra quyền (Admin, Giáo viên phụ trách môn học, hoặc Học viên đang có đăng ký môn học hợp lệ). Trả về binary stream với `Content-Type` chuẩn và `enableRangeProcessing: true`.
  - `DELETE /api/lesson-images/{id}`: Xóa blob vật lý và xóa metadata DB cho Giáo viên phụ trách hoặc Admin.
- **Tự động đồng bộ và dọn dẹp file:**
  - Khi thêm bài học mới: liên kết các ảnh vừa upload có ID nằm trong `Content` với `LessonId`.
  - Khi cập nhật bài học: dọn dẹp các ảnh mồ côi (ảnh đã từng thuộc bài học nhưng bị gỡ khỏi `Content`).
  - Khi xóa bài học: tự động xóa toàn bộ file blob của các ảnh thuộc bài học đó.
  - Khi admin xóa môn học: tự động xóa toàn bộ file blob của tất cả các bài học thuộc môn học đó.
- **Tích hợp trình soạn thảo Toast UI Editor:**
  - Cấu hình `addImageBlobHook` tự động upload file qua API khi dán ảnh (Ctrl+V), kéo thả hoặc dùng nút chèn ảnh.
  - Thay thế việc nhúng ảnh Base64 khổng lồ bằng đường dẫn API an toàn `![alt](/api/lesson-images/{id})`.
  - Hiển thị responsive trên cả giao diện xem trước của giáo viên và giao diện học tập của học viên.

## Các file đã thay đổi & tạo mới

1. **Entities & Database:**
   - [`LessonImage.cs`](file:///c:/Users/KIEN/Desktop/Project/LearningBff/LearningBff/Entities/LessonImage.cs)
   - [`Lesson.cs`](file:///c:/Users/KIEN/Desktop/Project/LearningBff/LearningBff/Entities/Lesson.cs)
   - [`LearningBffDbContext.cs`](file:///c:/Users/KIEN/Desktop/Project/LearningBff/LearningBff/Data/LearningBffDbContext.cs)
   - Migration: `20261018073448_Add_LessonImage_Entity.cs` (đã update database thành công).
2. **Services & Storage:**
   - [`ILessonImageStorageService.cs`](file:///c:/Users/KIEN/Desktop/Project/LearningBff/LearningBff/Services/LessonImages/ILessonImageStorageService.cs)
   - [`LessonImageFileStorageService.cs`](file:///c:/Users/KIEN/Desktop/Project/LearningBff/LearningBff/Services/LessonImages/LessonImageFileStorageService.cs)
   - [`LessonImageValidator.cs`](file:///c:/Users/KIEN/Desktop/Project/LearningBff/LearningBff/Services/LessonImages/LessonImageValidator.cs)
   - [`ILessonImageAppService.cs`](file:///c:/Users/KIEN/Desktop/Project/LearningBff/LearningBff/Services/LessonImages/ILessonImageAppService.cs)
   - [`LessonImageAppService.cs`](file:///c:/Users/KIEN/Desktop/Project/LearningBff/LearningBff/Services/LessonImages/LessonImageAppService.cs)
   - [`LessonImageDto.cs`](file:///c:/Users/KIEN/Desktop/Project/LearningBff/LearningBff/Dtos/LessonImageDto.cs)
   - [`LessonTeacherAppService.cs`](file:///c:/Users/KIEN/Desktop/Project/LearningBff/LearningBff/Services/Teacher/LessonTeacherAppService.cs) (tích hợp sync & delete)
   - [`SubjectAdminAppService.cs`](file:///c:/Users/KIEN/Desktop/Project/LearningBff/LearningBff/Services/Admin/SubjectAdminAppService.cs) (tích hợp dọn blob khi xóa môn)
3. **Controller & UI:**
   - [`LessonImagesController.cs`](file:///c:/Users/KIEN/Desktop/Project/LearningBff/LearningBff/Controllers/LessonImagesController.cs)
   - [`Curriculum.cshtml`](file:///c:/Users/KIEN/Desktop/Project/LearningBff/LearningBff/Pages/Teacher/Curriculum.cshtml) & [`Curriculum.cshtml.cs`](file:///c:/Users/KIEN/Desktop/Project/LearningBff/LearningBff/Pages/Teacher/Curriculum.cshtml.cs)
   - [`Subject.cshtml`](file:///c:/Users/KIEN/Desktop/Project/LearningBff/LearningBff/Pages/Learning/Subject.cshtml)
