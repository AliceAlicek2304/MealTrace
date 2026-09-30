# MealTrace

Bộ khung MVP cho hệ thống quản lý bữa ăn bán trú và truy vết dữ liệu báo cáo, dựa trên proposal. Dự án gồm:

- `fe/`: React 19, TypeScript, Vite, TanStack Query, Axios, Lucide.
- `be/src/MealTrace.Api/`: ASP.NET Core 8 Minimal API, EF Core 8, Npgsql/PostgreSQL, Swagger.
- `be/MealTrace.sln`: solution backend.

## Chạy trên Windows với PostgreSQL cài trực tiếp

Yêu cầu: .NET SDK 8, Node.js, PostgreSQL đang chạy. Máy hiện có dịch vụ `postgresql-x64-18`; không cần Docker.

1. Tạo database `mealtrace` bằng pgAdmin hoặc `psql` với tài khoản PostgreSQL của bạn:

   ```sql
   CREATE DATABASE mealtrace;
   ```

2. Lưu connection string vào .NET User Secrets (thay mật khẩu và port bằng cấu hình máy bạn):

   ```powershell
   dotnet user-secrets set "ConnectionStrings:MealTrace" "Host=localhost;Port=5432;Database=mealtrace;Username=postgres;Password=YOUR_PASSWORD" --project be/src/MealTrace.Api/MealTrace.Api.csproj
   ```

   User Secrets nằm ngoài repository. Khi triển khai, cung cấp biến môi trường `ConnectionStrings__MealTrace` hoặc secret manager tương đương.

3. Áp dụng EF migration và chạy API:

   ```powershell
   dotnet tool restore
   dotnet tool run dotnet-ef database update --project be/src/MealTrace.Api/MealTrace.Api.csproj --startup-project be/src/MealTrace.Api/MealTrace.Api.csproj
   dotnet run --project be/src/MealTrace.Api/MealTrace.Api.csproj --launch-profile http
   ```

   API: `http://localhost:5184`; Swagger: `http://localhost:5184/swagger`.

4. Mở terminal khác và chạy FE:

   ```powershell
   cd fe
   npm ci
   npm run dev
   ```

   FE: `http://localhost:5173`. Vite proxy `/api` sang backend.

## Hiện có trong bộ khung

- EF entities và migration đầu tiên cho lớp, học sinh, nguyên liệu/công thức theo phiên bản, thực đơn ngày ăn, đăng ký, chốt suất, minh chứng và snapshot báo cáo.
- API đọc ngày ăn, hồ sơ ngày ăn, lineage của report; endpoint health và Swagger.
- Giao diện tổng quan và chi tiết ngày ăn, có trạng thái loading/empty/error và lấy số liệu thật từ API.
- Bản ghi phiên bản và correction có các trường liên kết (`SupersedesId`, `AmendsId`) để giữ lịch sử thay vì ghi đè.

## Phạm vi cần làm tiếp

Đây là source nền tảng, chưa phải toàn bộ MVP trong proposal. Trước khi đưa dữ liệu thật vào hệ thống cần hoàn thiện đăng nhập và phân quyền theo vai trò/phạm vi học sinh; API ghi dữ liệu với kiểm tra cut-off và amendment; attendance, tính số suất; tính dinh dưỡng/chi phí từ đúng factor version; quy trình ký/recompute/so sánh report; upload ảnh và đồng bộ offline; học phí, thông báo và dashboard đầy đủ. Hiện API chỉ mở endpoint đọc, chưa có chức năng tạo dữ liệu từ FE. Các vai trò trong proposal có chỗ chưa thống nhất (Parent so với Teacher/Nutrition Officer), nên cần chốt ma trận quyền trước khi làm API ghi.
