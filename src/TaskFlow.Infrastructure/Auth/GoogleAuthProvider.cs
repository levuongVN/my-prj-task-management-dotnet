using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using TaskFlow.Application.Interfaces;

namespace TaskFlow.Infrastructure.Auth;

/// <summary>
/// Nhiệm vụ: xác thực rằng ID token FE gửi lên THẬT do Google phát hành
/// và cấp cho APP CỦA MÌNH, rồi trích email/name/picture từ token.
///
/// ID token là 1 JWT, payload chứa sẵn: email, name, picture, exp, aud...
/// Chuỗi token ai cũng đọc được (chỉ base64) nhưng KHÔNG forge được vì
/// chữ ký phải khớp public key của Google - đây là lý do dùng ID token an toàn.
/// </summary>
public class GoogleAuthProvider : IGoogleAuthProvider
{
    private readonly string _clientId;

    public GoogleAuthProvider(IConfiguration configuration)
    {
        // ID token là JWT do Google ký -> chỉ cần Client ID để check audience,
        // KHÔNG cần client secret (khác với GitHub flow). Auth giữa FE và
        // Google dựa vào "origin" đã khai Accepted JavaScript origins khi tạo Client ID.
        _clientId = configuration["Authentication:Google:ClientId"] ?? string.Empty;
    }

    public async Task<ExternalUserInfo> ValidateIdTokenAsync(string idToken)
    {
        // Thư viện tự thực hiện 3 lớp kiểm tra:
        //   1. Verify CHỮ KÝ token bằng public keys của Google (tự fetch JWKS)
        //      - chắc chắn token không phải attacker tự chế
        //   2. Verify thời hạn (claim exp) - ID token chỉ sống ~1 giờ
        //   3. Verify ISSUER (https://accounts.google.com)
        // Audience = [_clientId] là kiểm tra chúng ta tự BẮT BUỘC THÊM:
        //   claim 'aud' trong token phải == Client ID của app mình,
        //   để token cấp cho app khác không dùng giả chỗ này (app-juggling)
        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = [_clientId]
        };

        var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

        // EmailVerified = false nghĩa là Google chưa xác nhận người dùng sở hữu
        // email đó -> từ chối, vì toàn bộ hệ thống match account THEO EMAIL,
        // email chưa verified thì không đủ tin.
        if (!payload.EmailVerified)
        {
            throw new Exception("Google account email is not verified");
        }

        return new ExternalUserInfo
        {
            Email = payload.Email,
            FullName = payload.Name,
            AvatarUrl = payload.Picture
        };
    }
}
