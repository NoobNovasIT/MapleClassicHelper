# Assets

Hình ảnh dùng trong giao diện. App nhúng thư mục này vào exe dưới dạng WPF Resource
(xem `MapleHelper.App.csproj`), đường dẫn trong XAML: `/Assets/...`.

| Thư mục | Nội dung | Nguồn |
|---|---|---|
| `Sprites/Mobs/<tên>/<frame>.png` | Frame animation của quái (move, không có thì stand) | maplestory.io, dữ liệu GMS v83 |
| `Sprites/Npcs/` | NPC | maplestory.io |
| `Icons/Items/` | Icon vật phẩm | maplestory.io |
| `Icons/app.ico` | Icon app, sinh từ sprite Mushmom bằng `tools/make-icon.ps1` | |
| `Images/banner-characters.png` | Banner nhân vật trên trang tin | nexon.com/maplestory/news |

Thêm hoặc tải lại hình: sửa bảng id trong `tools/fetch-assets.ps1` rồi chạy

```
powershell -ExecutionPolicy Bypass -File tools/fetch-assets.ps1
```

## Bản quyền

Toàn bộ sprite, icon và hình ảnh MapleStory thuộc bản quyền của **NEXON Korea Corporation / NEXON America**.
Maple Classic Helper là công cụ do cộng đồng làm, không liên kết, không được Nexon tài trợ hay xác nhận.
Hình ảnh chỉ dùng để minh hoạ trong công cụ hỗ trợ người chơi, không dùng cho mục đích thương mại.

Trước khi phát hành rộng rãi cho cộng đồng, nên cân nhắc:
- Giữ dòng tuyên bố trên trong màn hình Giới thiệu của app.
- Nếu Nexon yêu cầu gỡ, thay thư mục này bằng hình tự vẽ là đủ, code không phụ thuộc vào hình cụ thể nào.
