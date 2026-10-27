import 'package:flutter/material.dart';
import '../domain/school_models.dart';
import 'school_controller.dart';
import 'school_widgets.dart';

class NotificationTestPage extends StatefulWidget {
  const NotificationTestPage({
    super.key,
    required this.repository,
    required this.roles,
    required this.onUnauthorized,
  });
  final SchoolRepository repository;
  final List<String> roles;
  final Future<void> Function() onUnauthorized;
  @override
  State<NotificationTestPage> createState() => _NotificationTestPageState();
}

class _NotificationTestPageState extends State<NotificationTestPage> {
  late final SchoolController controller;
  SchoolRecord? settings;
  String? error;
  bool loading = true, sending = false, confirming = false;
  @override
  void initState() {
    super.initState();
    controller = SchoolController(
      widget.repository,
      widget.roles,
      widget.onUnauthorized,
    );
    load();
  }

  @override
  void dispose() {
    controller.dispose();
    super.dispose();
  }

  Future<void> load() async {
    setState(() {
      loading = true;
      error = null;
      settings = null;
    });
    try {
      final result = await controller.execute(
        SchoolOperation.notificationSettings,
      );
      if (mounted) setState(() => settings = result.summary);
    } on SchoolFailure catch (failure) {
      if (mounted) setState(() => error = failure.message);
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  Future<void> send() async {
    if (sending ||
        confirming ||
        loading ||
        settings?.flag('enabled') != true ||
        !canExecute(SchoolOperation.sendTestNotification, widget.roles)) {
      return;
    }
    confirming = true;
    final yes = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Gửi tin thử'),
        content: const Text(
          'Gửi một tin đến số nhận thử đã thiết lập? Thao tác sử dụng hạn mức gửi tin.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Quay lại'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Gửi tin'),
          ),
        ],
      ),
    );
    confirming = false;
    if (!mounted || yes != true) return;
    setState(() => sending = true);
    try {
      final result = await controller.execute(
        SchoolOperation.sendTestNotification,
      );
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              result.summary.text('status') == 'ACCEPTED'
                  ? 'Đã tiếp nhận yêu cầu gửi. Kiểm tra tin nhắn trên điện thoại nhận.'
                  : displayValue(result.summary['status']),
            ),
          ),
        );
      }
    } on SchoolFailure catch (failure) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(failure.message)));
      }
    } finally {
      if (mounted) setState(() => sending = false);
    }
  }

  @override
  Widget build(BuildContext context) => PopScope(
    canPop: !sending,
    child: Scaffold(
      appBar: AppBar(title: const Text('Kiểm tra gửi tin')),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            if (loading) const LinearProgressIndicator(),
            if (error != null) ...[
              Text(error!),
              TextButton(onPressed: load, child: const Text('Thử lại')),
            ],
            if (settings != null) ...[
              ListTile(
                leading: const Icon(Icons.chat_outlined),
                title: const Text('Gửi thông báo'),
                subtitle: Text(
                  settings!.flag('enabled') ? 'Đã sẵn sàng' : 'Chưa sẵn sàng',
                ),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed:
                    settings!.flag('enabled') &&
                        !sending &&
                        canExecute(
                          SchoolOperation.sendTestNotification,
                          widget.roles,
                        )
                    ? send
                    : null,
                child: Text(sending ? 'Đang gửi…' : 'Gửi tin thử'),
              ),
            ],
          ],
        ),
      ),
    ),
  );
}
