class ParentOtpChallenge {
  const ParentOtpChallenge(
    this.id,
    this.expiresAt,
    this.resendAt,
    this.message,
  );
  final String id;
  final DateTime expiresAt;
  final DateTime resendAt;
  final String message;
}
