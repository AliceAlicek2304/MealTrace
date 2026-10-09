using System.Data;
using System.Security.Cryptography;
using System.Text;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Abstractions.Notifications;
using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Auth;
using MealTrace.Application.Features.Notifications;
using MealTrace.Domain.Entities;
using MealTrace.Domain.Security;

namespace MealTrace.Application.Features.Auth;

public sealed class ParentSignupOtpService(IAuthRepository repository, IUnitOfWork unitOfWork,
    IRegistrationOtpSender sender, NotificationPolicy policy, TimeProvider clock)
{
    public async Task<Result<ParentOtpResponse>> RequestAsync(RequestParentOtp request)
    {
        var phone = PhoneNumbers.Normalize(request.PhoneNumber);
        if (phone is null) return Result.Invalid("SĐT Việt Nam không hợp lệ.");
        if (!policy.Enabled || phone != PhoneNumbers.Normalize(policy.TestNumber))
            return Result.Invalid("WhatsApp đang ở chế độ thử: chỉ gửi OTP tới SĐT tester đã cấu hình và tham gia sandbox.");
        var now = clock.GetUtcNow();
        var code = RandomNumberGenerator.GetInt32(1000000).ToString("D6");
        ParentSignupOtp otp;
        await using (var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            if (await repository.FindAccountByPhoneAsync(phone) is not null)
                return Result.Conflict("SĐT đã có tài khoản. Vui lòng đăng nhập hoặc liên hệ nhà trường.");
            otp = await repository.FindSignupOtpAsync(phone) ?? new ParentSignupOtp { PhoneNumber = phone };
            if (otp.RequestedAt > now.AddSeconds(-60)) return Result.Conflict("Vui lòng đợi 60 giây trước khi gửi lại OTP.");
            if (otp.WindowStart <= now.AddHours(-1)) { otp.WindowStart = now; otp.SendsInWindow = 0; }
            if (otp.SendsInWindow >= 5) return Result.Conflict("SĐT đã đạt giới hạn gửi OTP. Vui lòng thử lại sau một giờ.");
            if (otp.ChallengeId == Guid.Empty) repository.AddSignupOtp(otp);
            otp.ChallengeId = Guid.NewGuid();
            otp.RequestedAt = now;
            otp.ExpiresAt = now.AddMinutes(5);
            otp.SendsInWindow++;
            otp.FailedAttempts = 0;
            otp.Used = false;
            otp.CodeHash = Hash(otp.ChallengeId, code);
            await unitOfWork.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        // Send after reservation commits; disconnects cannot create an unbounded resend loop.
        NotificationSendResult result;
        try { result = await sender.SendAsync("84" + phone[1..], code, CancellationToken.None); }
        catch (Exception) { result = new(NotificationOutcome.Unknown, null, "Chưa xác định kết quả gửi OTP."); }
        if (result.Outcome is NotificationOutcome.Failed or NotificationOutcome.Disabled)
            return Result.Invalid("Chưa gửi được OTP WhatsApp. Kiểm tra SĐT đã tham gia sandbox và cửa sổ hội thoại 24 giờ; chờ 60 giây trước khi gửi lại.");
        return Result.Success(new ParentOtpResponse(otp.ChallengeId, otp.ExpiresAt, now.AddSeconds(60),
            result.Outcome == NotificationOutcome.Unknown
                ? "Chưa xác định kết quả gửi. Nếu nhận OTP, bạn vẫn có thể nhập mã; không gửi lại ngay."
                : "Đã gửi yêu cầu OTP qua WhatsApp. Mã có hiệu lực 5 phút."));
    }

    public static bool Matches(ParentSignupOtp otp, string code) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(otp.CodeHash), Encoding.ASCII.GetBytes(Hash(otp.ChallengeId, code)));
    private static string Hash(Guid id, string code) => Convert.ToHexString(Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(code), id.ToByteArray(), 100000, HashAlgorithmName.SHA256, 32));
}
