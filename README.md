# Backend Interview Learning App (local MVP)

Ứng dụng đọc mục lục và nội dung học từ Notion qua ASP.NET Core. Frontend React/TypeScript chỉ gọi backend local; không ghi Notion, không lưu database riêng và không dùng OpenAI API.

## Yêu cầu

- .NET 8 SDK
- Node.js 20+ và npm
- Notion integration token lưu trong .NET User Secrets hoặc biến môi trường backend
- Share page `Kiến thức sâu` và các page con cần đọc với integration

## Chạy local

Mở hai terminal tại thư mục repo.

Terminal 1 — backend (chạy trong Terminal của Rider, tại thư mục repo):

```powershell
# Chỉ chạy một lần để lưu token ngoài repo:
dotnet user-secrets set "Notion:ApiToken" "<token của Notion integration chỉ đọc>" --project .\src\BackendInterview.Api\BackendInterview.Api.csproj

# Mỗi lần chạy backend:
dotnet run --project .\src\BackendInterview.Api\BackendInterview.Api.csproj -- --urls http://localhost:5080
```

User Secrets nằm ngoài repo. Không đặt token thật trong `appsettings.json`, `appsettings.Development.json` hoặc frontend. Có thể dùng biến môi trường `NOTION_API_TOKEN` thay cho User Secrets.

Terminal 2 — frontend (sau khi được phép cài dependencies):

```powershell
cd D:\PV\src\BackendInterview.Web
npm install
npm run dev
```

Vite gặp lỗi khi project root chứa `#` như `D:\C#\PV`. Tạo junction một lần để mở cùng repo qua đường dẫn không có `#` (chọn tên đích khác nếu `D:\PV` đã tồn tại):

```powershell
New-Item -ItemType Junction -Path D:\PV -Target 'D:\C#\PV'
```

Sau đó chạy terminal frontend từ `D:\PV\src\BackendInterview.Web`. Script `dev` giữ đường dẫn junction cho Node/Vite.

Mở `http://127.0.0.1:5173`. Frontend proxy `/api` tới backend tại `http://localhost:5080`.

## API local

- `GET /api/health`
- `GET /api/roadmap`
- `GET /api/topics/{pageId}`
- `GET /api/progress`
- `GET /api/reviews?topicId={pageId}`

Notion response được cache trong memory 120 giây (có thể đổi bằng `Notion:CacheSeconds`). Checkbox không tồn tại thì progress trả trạng thái chưa có dữ liệu. Page không đọc được giữ nguyên card và hiện lỗi tải riêng.

## Giới hạn hiện tại

- Integration cần quyền đọc toàn bộ page; hai topic hiện trả 404 với connector dùng để khảo sát và cần kiểm tra quyền lại bằng token backend.
- Parser giữ cấu trúc block cơ bản và link nguồn; nhận diện review theo ngày hoặc dấu hiệu review nên cần đối chiếu với nội dung Notion thực tế.
- Chưa chạy build, typecheck, test hay browser check trong lượt implementation này.
