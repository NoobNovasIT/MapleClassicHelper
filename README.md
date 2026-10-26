# Maple Classic Helper

Công cụ overlay giúp người chơi Việt Nam chơi **MapleStory Classic World** dễ hơn:

- **Điểm tin**: tin tức chính thức từ nexon.com (lọc riêng Classic World), tự dịch sang tiếng Việt, giờ bảo trì quy đổi sang giờ VN.
- **Overlay trên game**: nút nấm cam nổi trên màn hình, bấm bung 4 nút:
  1. **Dịch vùng chọn**: kéo chọn hội thoại, quest, thông báo… để dịch ngay (OCR của Windows + Google Dịch, khoảng 1–2 giây; có tuỳ chọn dùng AI).
  2. **Tìm nguồn item**: chụp tooltip vật phẩm, tra quái rơi, quest thưởng, nơi mua.
  3. **Hỗ trợ quest**: chụp tên nhiệm vụ, nhận hướng dẫn từng bước và link video.
  4. **Chat Việt → Anh**: gõ tiếng Việt, Enter, câu tiếng Anh được copy sẵn để dán vào ô chat (dùng Google Dịch, không tốn quota AI).
- Hotkey toàn cục cho từng chức năng (mặc định `Ctrl+Shift+M` và `Ctrl+Shift+1..4`), đổi được trong Cài đặt.
- Chọn được nhà cung cấp AI: **Gemini** (mặc định, có free tier), OpenAI, Claude, DeepSeek (chỉ text).

> Overlay chỉ hiện trên game khi game chạy ở **chế độ cửa sổ**: Options → Graphics → chọn độ phân giải không có chữ "Full Screen".

## Cấu trúc

```
src/
  MapleHelper.App/             WPF: cửa sổ chính, overlay, popup, Velopack
  MapleHelper.Core/            Model, interface, logic (không phụ thuộc WPF)
  MapleHelper.Infrastructure/  AI provider, SQLite, nguồn tin Nexon, maplestory.io
  MapleHelper.Native/          Win32: hotkey, chụp màn hình, kiểu cửa sổ overlay
tests/                         xUnit v3
Assets/                        Sprite, icon (xem Assets/README.md về bản quyền)
tools/                         Script tải sprite, tạo icon
build/publish.ps1              Đóng gói Setup.exe bằng Velopack
```

Dữ liệu người dùng: `%AppData%\MapleClassicHelper` (settings.json, secrets.dat mã hoá DPAPI, data.db, logs).

## Build

Cần .NET SDK 10.

```bash
dotnet build MapleClassicHelper.sln
dotnet test --solution MapleClassicHelper.sln
```

## Phát hành

```bash
powershell -ExecutionPolicy Bypass -File build/publish.ps1 -Version 0.2.0
```

Bộ cài nằm ở `Releases/MapleClassicHelper-win-Setup.exe`. Để app tự cập nhật: push tag `v0.2.0` lên GitHub
(workflow `.github/workflows/release.yml` build và đăng lên Releases), rồi nhập URL repo trong Cài đặt → Cập nhật.

## Nguồn dữ liệu

- Tin tức: `https://g.nexonstatic.com/maplestory/cms/v1/news` (JSON mà trang nexon.com/maplestory/news dùng).
- Vật phẩm, quest, sprite: [maplestory.io](https://maplestory.io) (GMS v83, gần với Classic World nhất).

## Tuyên bố

Công cụ do cộng đồng làm, không liên kết, không được Nexon tài trợ hay xác nhận.
MapleStory và các hình ảnh liên quan thuộc bản quyền NEXON Korea Corporation.
