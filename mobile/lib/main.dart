import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';

void main() => runApp(const MealTraceApp());

const _green = Color(0xFF28745B);
const _ink = Color(0xFF18352C);
const _paper = Color(0xFFF5F7F3);

class MealTraceApp extends StatelessWidget {
  const MealTraceApp({super.key});

  @override
  Widget build(BuildContext context) => MaterialApp(
    title: 'MealTrace',
    debugShowCheckedModeBanner: false,
    theme: ThemeData(
      useMaterial3: true,
      scaffoldBackgroundColor: _paper,
      colorScheme: ColorScheme.fromSeed(seedColor: _green),
      appBarTheme: const AppBarTheme(
        backgroundColor: _paper,
        foregroundColor: _ink,
        surfaceTintColor: Colors.transparent,
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: Colors.white,
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(14)),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(14),
          borderSide: const BorderSide(color: Color(0xFFE0E8E1)),
        ),
      ),
    ),
    home: const LoginPage(),
  );
}

class Api {
  Api(this.baseUrl);
  final String baseUrl;
  String? token;

  Future<dynamic> request(
    String method,
    String path, {
    Object? body,
    bool auth = true,
  }) async {
    final uri = Uri.parse('$baseUrl/api$path');
    final client = HttpClient()..connectionTimeout = const Duration(seconds: 10);
    try {
      final req = await client.openUrl(method, uri);
      req.headers.set(HttpHeaders.acceptHeader, 'application/json');
      if (body != null) req.headers.contentType = ContentType.json;
      if (auth && token != null) {
        req.headers.set(HttpHeaders.authorizationHeader, 'Bearer $token');
      }
      if (body != null) req.write(jsonEncode(body));
      final response = await req.close();
      final text = await response.transform(utf8.decoder).join();
      dynamic data;
      if (text.isNotEmpty) {
        try {
          data = jsonDecode(text);
        } catch (_) {
          data = null;
        }
      }
      if (response.statusCode < 200 || response.statusCode >= 300) {
        final message = data is Map ? data['message'] : null;
        throw ApiException(
          message?.toString() ?? _statusMessage(response.statusCode),
        );
      }
      return data;
    } on SocketException {
      throw ApiException(
        'Không kết nối được API. Kiểm tra địa chỉ máy chủ và mạng.',
      );
    } on HandshakeException {
      throw ApiException('Lỗi kết nối bảo mật SSL/TLS với máy chủ.');
    } on FormatException {
      throw ApiException('Phản hồi API không hợp lệ.');
    } finally {
      client.close(force: true);
    }
  }

  static String _statusMessage(int status) => switch (status) {
    401 => 'Số điện thoại/email hoặc mật khẩu không đúng.',
    403 => 'Tài khoản không có quyền xem dữ liệu này.',
    409 => 'Dữ liệu vừa thay đổi hoặc đã qua giờ chốt.',
    429 => 'Thử đăng nhập quá nhanh. Vui lòng đợi một phút.',
    _ => 'Máy chủ trả về lỗi ($status).',
  };
}

class ApiException implements Exception {
  ApiException(this.message);
  final String message;
  @override
  String toString() => message;
}

class CurrentUser {
  CurrentUser.fromJson(Map<String, dynamic> json)
    : id = (json['id'] as String?) ?? '',
      fullName = (json['fullName'] as String?) ?? '',
      email = (json['email'] as String?) ?? '',
      roles = List<String>.from(json['roles'] as List? ?? const []);
  final String id;
  final String fullName;
  final String email;
  final List<String> roles;
  bool has(String role) => roles.contains(role);
}

class LoginPage extends StatefulWidget {
  const LoginPage({super.key});
  @override
  State<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends State<LoginPage> {
  final _form = GlobalKey<FormState>();
  final _identifier = TextEditingController();
  final _password = TextEditingController();
  final _apiUrl = TextEditingController(text: 'http://10.0.2.2:5184');
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _identifier.dispose();
    _password.dispose();
    _apiUrl.dispose();
    super.dispose();
  }

  Future<void> _login() async {
    if (!_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    final api = Api(_apiUrl.text.trim().replaceFirst(RegExp(r'/+$'), ''));
    try {
      final result = await api.request(
        'POST',
        '/auth/login',
        auth: false,
        body: {
          'identifier': _identifier.text.trim(),
          'password': _password.text,
        },
      );
      if (result is! Map<String, dynamic> || result['accessToken'] == null) {
        throw ApiException('Phản hồi đăng nhập không hợp lệ từ máy chủ.');
      }
      api.token = result['accessToken'] as String;
      final userData = result['user'];
      if (userData is! Map<String, dynamic>) {
        throw ApiException('Không tìm thấy thông tin người dùng.');
      }
      final user = CurrentUser.fromJson(userData);
      if (!mounted) return;
      Navigator.of(context).pushReplacement(
        MaterialPageRoute(
          builder: (_) => HomePage(api: api, user: user),
        ),
      );
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    } catch (_) {
      setState(
        () => _error = 'Không thể đăng nhập. Kiểm tra thông tin và thử lại.',
      );
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    body: SafeArea(
      child: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 460),
            child: Form(
              key: _form,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Container(
                    width: 58,
                    height: 58,
                    decoration: BoxDecoration(
                      color: const Color(0xFFE3F1E8),
                      borderRadius: BorderRadius.circular(20),
                    ),
                    child: const Icon(
                      Icons.eco_rounded,
                      color: _green,
                      size: 30,
                    ),
                  ),
                  const SizedBox(height: 24),
                  Text(
                    'MealTrace',
                    style: Theme.of(context).textTheme.headlineMedium
                        ?.copyWith(fontWeight: FontWeight.w800, color: _ink),
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Bữa ăn minh bạch, chăm sóc trọn vẹn.',
                    style: TextStyle(color: Color(0xFF68786E)),
                  ),
                  const SizedBox(height: 32),
                  _FieldLabel('Số điện thoại hoặc email'),
                  TextFormField(
                    controller: _identifier,
                    keyboardType: TextInputType.emailAddress,
                    textInputAction: TextInputAction.next,
                    decoration: const InputDecoration(
                      hintText: 'Nhập số điện thoại hoặc email',
                    ),
                    validator: (value) => value == null || value.trim().isEmpty
                        ? 'Vui lòng nhập tài khoản.'
                        : null,
                  ),
                  const SizedBox(height: 18),
                  _FieldLabel('Mật khẩu'),
                  TextFormField(
                    controller: _password,
                    obscureText: true,
                    textInputAction: TextInputAction.done,
                    onFieldSubmitted: (_) => _login(),
                    decoration: const InputDecoration(
                      hintText: 'Nhập mật khẩu',
                    ),
                    validator: (value) => value == null || value.isEmpty
                        ? 'Vui lòng nhập mật khẩu.'
                        : null,
                  ),
                  const SizedBox(height: 18),
                  ExpansionTile(
                    tilePadding: EdgeInsets.zero,
                    childrenPadding: const EdgeInsets.only(bottom: 12),
                    title: const Text(
                      'Cấu hình kết nối API',
                      style: TextStyle(fontSize: 14),
                    ),
                    children: [
                      TextFormField(
                        controller: _apiUrl,
                        keyboardType: TextInputType.url,
                        decoration: const InputDecoration(
                          labelText: 'Địa chỉ máy chủ',
                          hintText: 'http://10.0.2.2:5184',
                        ),
                      ),
                    ],
                  ),
                  if (_error != null) ...[
                    const SizedBox(height: 8),
                    _ErrorCard(_error!),
                  ],
                  const SizedBox(height: 16),
                  FilledButton(
                    onPressed: _busy ? null : _login,
                    style: FilledButton.styleFrom(
                      minimumSize: const Size.fromHeight(54),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(15),
                      ),
                    ),
                    child: _busy
                        ? const SizedBox.square(
                            dimension: 22,
                            child: CircularProgressIndicator(
                              strokeWidth: 2,
                              color: Colors.white,
                            ),
                          )
                        : const Text('Đăng nhập'),
                  ),
                  const SizedBox(height: 20),
                  const Text(
                    'Đăng nhập bằng tài khoản do nhà trường cấp.',
                    textAlign: TextAlign.center,
                    style: TextStyle(color: Color(0xFF7B8981), fontSize: 12),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    ),
  );
}

class HomePage extends StatefulWidget {
  const HomePage({super.key, required this.api, required this.user});
  final Api api;
  final CurrentUser user;
  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> {
  int _tab = 0;
  bool _loading = false;
  String? _error;
  List<dynamic> _items = [];
  List<dynamic> _decisions = [];
  bool _canEditDecisions = false;
  Map<String, dynamic>? _selectedPortions;

  bool get _isParent => widget.user.has('PARENT');
  bool get _isStaff =>
      widget.user.has('ADMIN') ||
      widget.user.has('TEACHER') ||
      widget.user.has('KITCHEN_STAFF');
  bool get _canManageExceptions =>
      widget.user.has('ADMIN') || widget.user.has('TEACHER');

  List<String> get _tabs {
    if (_isParent) return ['Tổng quan', 'Báo vắng'];
    if (_isStaff) return ['Tổng quan', 'Số suất'];
    return ['Tổng quan'];
  }

  String get _greetingName {
    final name = widget.user.fullName.trim();
    if (name.isEmpty) return 'bạn';
    final parts = name.split(RegExp(r'\s+'));
    return parts.last;
  }

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      if (_selectedPortions != null) {
        final dayId = _selectedPortions!['id'] as String?;
        if (dayId != null) {
          await _openPortions({'id': dayId});
          return;
        }
      }
      if (_isParent) {
        _items = await widget.api.request(
          'GET',
          '/parent/absences',
        ) as List<dynamic>? ?? [];
      } else if (_isStaff) {
        _items = await widget.api.request(
          'GET',
          '/meal-days/workflow',
        ) as List<dynamic>? ?? [];
      }
    } on ApiException catch (e) {
      _error = e.message;
    } catch (_) {
      _error = 'Không tải được dữ liệu. Hãy thử tải lại.';
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _openPortions(Map<String, dynamic> day) async {
    setState(() {
      _loading = true;
      _selectedPortions = null;
      _error = null;
    });
    try {
      final portions = await widget.api.request(
        'GET',
        '/meal-days/${day['id']}/portions',
      );
      if (portions is! Map<String, dynamic>) {
        throw ApiException('Dữ liệu suất ăn không hợp lệ.');
      }
      _selectedPortions = portions;
      if (_canManageExceptions) {
        final decisions = await widget.api.request(
          'GET',
          '/meal-days/${day['id']}/decisions',
        );
        if (decisions is Map<String, dynamic>) {
          _decisions = decisions['items'] as List<dynamic>? ?? [];
          _canEditDecisions = decisions['canEdit'] == true;
        } else {
          _decisions = [];
          _canEditDecisions = false;
        }
      } else {
        _decisions = [];
        _canEditDecisions = false;
      }
    } on ApiException catch (e) {
      _error = e.message;
    } catch (_) {
      _error = 'Không thể tải chi tiết phiên ăn.';
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _logout() async {
    try {
      await widget.api.request('POST', '/auth/logout');
    } catch (_) {}
    widget.api.token = null;
    if (mounted) {
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(builder: (_) => const LoginPage()),
        (_) => false,
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final tabs = _tabs;
    return Scaffold(
      appBar: AppBar(
        title: Row(
          children: [
            const Icon(Icons.eco_rounded, color: _green),
            const SizedBox(width: 9),
            const Text(
              'MealTrace',
              style: TextStyle(fontWeight: FontWeight.w800),
            ),
            const Spacer(),
            IconButton(
              onPressed: _load,
              tooltip: 'Tải lại',
              icon: const Icon(Icons.refresh_rounded),
            ),
            PopupMenuButton<String>(
              onSelected: (_) => _logout(),
              itemBuilder: (_) => const [
                PopupMenuItem(value: 'logout', child: Text('Đăng xuất')),
              ],
            ),
          ],
        ),
      ),
      body: RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(20, 12, 20, 28),
          children: [
            _greeting(),
            const SizedBox(height: 20),
            if (_error != null) _ErrorCard(_error!),
            if (_loading) const LinearProgressIndicator(),
            if (_selectedPortions != null) _portionsPanel(),
            if (_selectedPortions == null && !_loading && _error == null) ...[
              if (_tab == 0) _overview(tabs),
              if (_tab == 1 && _isParent) _parentAbsences(),
              if (_tab == 1 && _isStaff) _mealDays(),
            ],
          ],
        ),
      ),
      bottomNavigationBar: tabs.length > 1
          ? NavigationBar(
              selectedIndex: _tab,
              onDestinationSelected: (value) => setState(() {
                _tab = value;
                _selectedPortions = null;
              }),
              destinations: [
                for (final tab in tabs)
                  NavigationDestination(
                    icon: Icon(
                      tab == 'Báo vắng'
                          ? Icons.event_busy_outlined
                          : tab == 'Số suất'
                          ? Icons.restaurant_menu_rounded
                          : Icons.home_outlined,
                    ),
                    selectedIcon: Icon(
                      tab == 'Báo vắng'
                          ? Icons.event_busy
                          : tab == 'Số suất'
                          ? Icons.restaurant_menu
                          : Icons.home_rounded,
                    ),
                    label: tab,
                  ),
              ],
            )
          : null,
    );
  }

  Widget _greeting() => Container(
    padding: const EdgeInsets.all(20),
    decoration: BoxDecoration(
      color: _green,
      borderRadius: BorderRadius.circular(24),
    ),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          _roleLabel,
          style: const TextStyle(
            color: Color(0xFFD3E9D9),
            fontSize: 12,
            fontWeight: FontWeight.w700,
            letterSpacing: 1,
          ),
        ),
        const SizedBox(height: 10),
        Text(
          'Xin chào, $_greetingName',
          style: const TextStyle(
            color: Colors.white,
            fontSize: 23,
            fontWeight: FontWeight.w800,
          ),
        ),
        const SizedBox(height: 6),
        const Text(
          'Cùng theo dõi bữa ăn hôm nay nhé.',
          style: TextStyle(color: Color(0xFFE2F0E6)),
        ),
      ],
    ),
  );

  String get _roleLabel => _isParent
      ? 'KHÔNG GIAN PHỤ HUYNH'
      : widget.user.has('KITCHEN_STAFF')
      ? 'KHÔNG GIAN NHÀ BẾP'
      : widget.user.has('TEACHER')
      ? 'KHÔNG GIAN GIÁO VIÊN'
      : 'KHÔNG GIAN NHÀ TRƯỜNG';

  Widget _overview(List<String> tabs) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Text(
        _isParent ? 'Theo dõi của gia đình' : 'Vận hành bữa ăn',
        style: Theme.of(context).textTheme.titleLarge
            ?.copyWith(color: _ink, fontWeight: FontWeight.w800),
      ),
      const SizedBox(height: 12),
      if (_isParent) ...[
        _InfoTile(
          icon: Icons.restaurant_rounded,
          title: 'Thực đơn và bữa ăn',
          subtitle: 'Thông tin đã công bố cho trẻ sẽ hiển thị tại đây.',
          color: const Color(0xFFE3F1E8),
        ),
        const SizedBox(height: 10),
        _InfoTile(
          icon: Icons.event_busy_rounded,
          title: 'Báo vắng / Không ăn',
          subtitle: 'Khai báo khoảng ngày để nhà trường chuẩn bị suất ăn.',
          color: const Color(0xFFFFEEDB),
          onTap: () => setState(() => _tab = 1),
        ),
        const SizedBox(height: 10),
        _InfoTile(
          icon: Icons.child_care_rounded,
          title: 'Trẻ liên kết',
          subtitle: 'Dữ liệu chỉ hiển thị theo quan hệ phụ huynh – trẻ trên tài khoản.',
          color: const Color(0xFFE9E7F7),
        ),
      ] else ...[
        _InfoTile(
          icon: Icons.restaurant_menu_rounded,
          title: 'Số suất theo phiên ăn',
          subtitle: 'Xem danh sách dự kiến, số suất đã chốt và lớp.',
          color: const Color(0xFFE3F1E8),
          onTap: () => setState(() => _tab = 1),
        ),
        if (_canManageExceptions) ...[
          const SizedBox(height: 10),
          _InfoTile(
            icon: Icons.fact_check_outlined,
            title: 'Ngoại lệ trước giờ chốt',
            subtitle: 'Ghi nhận trẻ có suất, vắng hoặc khôi phục mặc định.',
            color: const Color(0xFFFFEEDB),
            onTap: () => setState(() => _tab = 1),
          ),
        ],
        if (widget.user.has('ADMIN')) ...[
          const SizedBox(height: 10),
          _InfoTile(
            icon: Icons.calendar_month_outlined,
            title: 'Quản lý lớp và tài khoản',
            subtitle: 'Các thao tác quản trị chi tiết hiện dùng giao diện web.',
            color: const Color(0xFFE9E7F7),
          ),
        ],
      ],
      const SizedBox(height: 20),
      const _SectionTitle('Tài khoản'),
      const SizedBox(height: 10),
      _InfoTile(
        icon: Icons.person_outline_rounded,
        title: widget.user.fullName,
        subtitle: widget.user.email.isEmpty
            ? widget.user.roles.join(' · ')
            : widget.user.email,
        color: const Color(0xFFE9EEF1),
      ),
    ],
  );

  Widget _mealDays() {
    if (_selectedPortions != null) return const SizedBox.shrink();
    if (_items.isEmpty && !_loading) {
      return _EmptyState(
        title: 'Chưa có phiên ăn',
        body: 'Các phiên ăn đã tạo sẽ hiển thị ở đây.',
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const _SectionTitle('Phiên ăn gần đây'),
        const SizedBox(height: 10),
        for (final raw in _items)
          Builder(
            builder: (context) {
              final day = raw as Map<String, dynamic>;
              final settled = day['isSettled'] == true;
              return Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: _InfoTile(
                  icon: Icons.restaurant_rounded,
                  title: '${day['mealType']} · ${day['date']}',
                  subtitle:
                      '${day['schoolYear'] ?? 'Chưa gắn niên khóa'} · ${settled ? 'Đã chốt' : 'Đang dự kiến'}',
                  color: settled
                      ? const Color(0xFFE3F1E8)
                      : const Color(0xFFFFEEDB),
                  trailing: const Icon(Icons.chevron_right_rounded),
                  onTap: () => _openPortions(day),
                ),
              );
            },
          ),
        const SizedBox(height: 8),
        const Text(
          'Giờ chốt suất: 07:30 (UTC+7). Bản đã chốt giữ nguyên lịch sử.',
          style: TextStyle(color: Color(0xFF68786E), fontSize: 12),
        ),
      ],
    );
  }

  Widget _portionsPanel() {
    final data = _selectedPortions!;
    final rooms = data['classes'] as List? ?? const [];
    final total = rooms.fold<int>(
      0,
      (sum, item) =>
          sum +
          ((item['studentIds'] as List?)?.length ??
              (item['studentNames'] as List?)?.length ??
              0),
    );
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        TextButton.icon(
          onPressed: () => setState(() => _selectedPortions = null),
          icon: const Icon(Icons.arrow_back),
          label: const Text('Danh sách phiên ăn'),
        ),
        Text(
          '${data['mealType']} · ${data['date']}',
          style: Theme.of(context).textTheme.titleLarge
              ?.copyWith(color: _ink, fontWeight: FontWeight.w800),
        ),
        const SizedBox(height: 6),
        Text(
          '$total suất dự kiến · Giờ chốt ${data['cutoffAt'] ?? ''}',
          style: const TextStyle(color: Color(0xFF68786E)),
        ),
        const SizedBox(height: 14),
        for (final raw in rooms)
          Builder(
            builder: (context) {
              final room = raw as Map<String, dynamic>;
              final names = room['studentNames'] as List? ?? const [];
              final absent = room['absentStudentIds'] as List? ?? const [];
              return Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: Container(
                  padding: const EdgeInsets.all(16),
                  decoration: BoxDecoration(
                    color: Colors.white,
                    borderRadius: BorderRadius.circular(18),
                    border: Border.all(color: const Color(0xFFE4EAE4)),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        '${room['className']} · ${names.length} suất',
                        style: const TextStyle(
                          fontWeight: FontWeight.w800,
                          color: _ink,
                        ),
                      ),
                      const SizedBox(height: 5),
                      Text(
                        room['isSettled'] == true
                            ? 'Bản đã chốt'
                            : '${absent.length} trẻ không có suất dự kiến',
                        style: const TextStyle(
                          color: Color(0xFF68786E),
                          fontSize: 12,
                        ),
                      ),
                      if (names.isNotEmpty) ...[
                        const SizedBox(height: 10),
                        Text(
                          names.join(', '),
                          style: const TextStyle(height: 1.45),
                        ),
                      ],
                    ],
                  ),
                ),
              );
            },
          ),
        if (_canManageExceptions) ...[
          const SizedBox(height: 10),
          const _SectionTitle('Nguồn dự kiến ăn và ngoại lệ'),
          const SizedBox(height: 6),
          const Text(
            'Dự kiến ăn không phải xác nhận có mặt thực tế.',
            style: TextStyle(color: Color(0xFF68786E), fontSize: 12),
          ),
          if (!_canEditDecisions)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 10),
              child: Text(
                'Đã qua giờ chốt hoặc phiên đã chốt; chỉ xem thông tin.',
                style: TextStyle(color: Color(0xFF68786E)),
              ),
            ),
          for (final raw in _decisions)
            Builder(
              builder: (context) {
                final item = raw as Map<String, dynamic>;
                final eating = item['willEat'] == true;
                return Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: _InfoTile(
                    icon: eating
                        ? Icons.check_circle_outline_rounded
                        : Icons.remove_circle_outline_rounded,
                    title: '${item['fullName']} · ${item['className']}',
                    subtitle:
                        '${eating ? 'Có suất' : 'Không có suất'}${item['latestReason'] == null ? '' : ' · ${item['latestReason']}'}',
                    color: eating
                        ? const Color(0xFFE3F1E8)
                        : const Color(0xFFFFEEDB),
                    trailing: _canEditDecisions
                        ? IconButton(
                            onPressed: () => _recordException(item),
                            icon: const Icon(Icons.edit_outlined),
                            tooltip: 'Ghi ngoại lệ',
                          )
                        : null,
                  ),
                );
              },
            ),
        ],
      ],
    );
  }

  Future<void> _recordException(Map<String, dynamic> item) async {
    final reason = TextEditingController();
    var action = item['willEat'] == true ? 'ABSENT' : 'EAT';
    String? dialogError;
    final portionId = _selectedPortions?['id'] as String?;
    if (portionId == null) return;

    final saved = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (context, setDialogState) => AlertDialog(
          title: Text('Ngoại lệ: ${item['fullName']}'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              DropdownButtonFormField<String>(
                initialValue: action,
                decoration: const InputDecoration(labelText: 'Thao tác'),
                items: const [
                  DropdownMenuItem(
                    value: 'EAT',
                    child: Text('Dự kiến có suất'),
                  ),
                  DropdownMenuItem(
                    value: 'ABSENT',
                    child: Text('Dự kiến không có suất'),
                  ),
                  DropdownMenuItem(
                    value: 'DEFAULT',
                    child: Text('Khôi phục mặc định'),
                  ),
                ],
                onChanged: (value) {
                  if (value != null) setDialogState(() => action = value);
                },
              ),
              const SizedBox(height: 12),
              TextField(
                controller: reason,
                maxLength: 500,
                maxLines: 2,
                decoration: InputDecoration(
                  labelText: 'Lý do',
                  errorText: dialogError,
                ),
                onChanged: (_) {
                  if (dialogError != null) {
                    setDialogState(() => dialogError = null);
                  }
                },
              ),
              const Text(
                'Khôi phục mặc định vẫn áp dụng báo vắng của phụ huynh.',
                style: TextStyle(fontSize: 12),
              ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(dialogContext, false),
              child: const Text('Hủy'),
            ),
            FilledButton(
              onPressed: () async {
                if (reason.text.trim().isEmpty) {
                  setDialogState(() => dialogError = 'Vui lòng nhập lý do.');
                  return;
                }
                try {
                  await widget.api.request(
                    'POST',
                    '/meal-days/$portionId/exceptions',
                    body: {
                      'studentId': item['studentId'],
                      'action': action,
                      'reason': reason.text.trim(),
                      'expectedEventId': item['latestEventId'],
                    },
                  );
                  if (dialogContext.mounted) Navigator.pop(dialogContext, true);
                } on ApiException catch (e) {
                  setDialogState(() => dialogError = e.message);
                } catch (_) {
                  setDialogState(() => dialogError = 'Lỗi không xác định.');
                }
              },
              child: const Text('Lưu'),
            ),
          ],
        ),
      ),
    );
    reason.dispose();
    if (saved == true && mounted) {
      await _openPortions({'id': portionId});
    }
  }

  Widget _parentAbsences() {
    if (_items.isEmpty) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const _SectionTitle('Báo vắng / Không ăn'),
          const SizedBox(height: 10),
          _InfoTile(
            icon: Icons.event_available_outlined,
            title: 'Chưa có đăng ký',
            subtitle: 'Tạo đăng ký không ăn theo ngày hoặc khoảng ngày.',
            color: const Color(0xFFE3F1E8),
          ),
          const SizedBox(height: 12),
          FilledButton.icon(
            onPressed: _newAbsence,
            icon: const Icon(Icons.add),
            label: const Text('Tạo đăng ký'),
          ),
        ],
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          children: [
            const Expanded(child: _SectionTitle('Đăng ký đã gửi')),
            IconButton(
              onPressed: _newAbsence,
              icon: const Icon(Icons.add_circle_outline),
              tooltip: 'Tạo đăng ký',
            ),
          ],
        ),
        for (final raw in _items)
          Builder(
            builder: (context) {
              final item = raw as Map<String, dynamic>;
              final cancelled = item['cancelledAt'] != null;
              return Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: _InfoTile(
                  icon: Icons.event_busy_outlined,
                  title:
                      '${item['studentName']} · ${item['fromDate']} – ${item['toDate']}',
                  subtitle:
                      '${item['reason']} · ${cancelled ? 'Đã hủy/thay thế' : 'Đang hiệu lực'}',
                  color: cancelled
                      ? const Color(0xFFE9EEF1)
                      : const Color(0xFFFFEEDB),
                  trailing: !cancelled
                      ? PopupMenuButton<String>(
                          onSelected: (value) => value == 'cancel'
                              ? _cancelAbsence(item['id'] as String)
                              : _editAbsence(item),
                          itemBuilder: (_) => const [
                            PopupMenuItem(
                              value: 'edit',
                              child: Text('Sửa khoảng ngày'),
                            ),
                            PopupMenuItem(
                              value: 'cancel',
                              child: Text('Hủy / Ăn lại'),
                            ),
                          ],
                        )
                      : null,
                ),
              );
            },
          ),
        const SizedBox(height: 8),
        const Text(
          'Bản suất đã qua giờ chốt sẽ không thay đổi khi cập nhật báo vắng.',
          style: TextStyle(color: Color(0xFF68786E), fontSize: 12),
        ),
      ],
    );
  }

  Future<void> _newAbsence() async => _absenceForm();

  Future<void> _editAbsence(Map<String, dynamic> item) async =>
      _absenceForm(item: item);

  Future<void> _absenceForm({Map<String, dynamic>? item}) async {
    List<dynamic> children;
    try {
      final res = await widget.api.request('GET', '/parent/students');
      children = res is List<dynamic> ? res : [];
    } on ApiException catch (e) {
      if (mounted) _showMessage(e.message);
      return;
    } catch (_) {
      if (mounted) _showMessage('Không thể tải danh sách trẻ.');
      return;
    }
    if (!mounted) return;
    final formKey = GlobalKey<FormState>();
    final reason = TextEditingController(
      text: item?['reason'] as String? ?? '',
    );

    final childItems = children.whereType<Map<String, dynamic>>().toList();
    final childIds = childItems
        .map((c) => (c['studentId'] ?? c['id'])?.toString())
        .whereType<String>()
        .toList();

    String? studentId = item?['studentId'] as String?;
    if (studentId == null || !childIds.contains(studentId)) {
      studentId = childIds.isNotEmpty ? childIds.first : null;
    }

    final now = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);
    DateTime from = _parseDate(item?['fromDate'] as String?) ?? today;
    DateTime to = _parseDate(item?['toDate'] as String?) ?? from;

    final saved = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (sheetContext) => StatefulBuilder(
        builder: (context, setSheetState) => Padding(
          padding: EdgeInsets.fromLTRB(
            20,
            8,
            20,
            MediaQuery.of(context).viewInsets.bottom + 24,
          ),
          child: Form(
            key: formKey,
            child: SingleChildScrollView(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    item == null ? 'Đăng ký không ăn' : 'Sửa khoảng không ăn',
                    style: Theme.of(context).textTheme.titleLarge
                        ?.copyWith(fontWeight: FontWeight.w800, color: _ink),
                  ),
                  const SizedBox(height: 16),
                  DropdownButtonFormField<String>(
                    initialValue: studentId,
                    decoration: const InputDecoration(labelText: 'Trẻ'),
                    items: [
                      for (final child in childItems)
                        if ((child['studentId'] ?? child['id']) != null)
                          DropdownMenuItem(
                            value: (child['studentId'] ?? child['id']).toString(),
                            child: Text(
                              '${child['fullName'] ?? 'Trẻ'}${child['className'] == null ? '' : ' · ${child['className']}'}',
                            ),
                          ),
                    ],
                    onChanged: item == null
                        ? (value) => setSheetState(() => studentId = value)
                        : null,
                    validator: (v) =>
                        v == null ? 'Chưa có trẻ liên kết.' : null,
                  ),
                  const SizedBox(height: 12),
                  _DatePickerField(
                    label: 'Từ ngày',
                    value: from,
                    onChanged: (value) => setSheetState(() {
                      from = value;
                      if (to.isBefore(from)) to = from;
                    }),
                  ),
                  const SizedBox(height: 12),
                  _DatePickerField(
                    label: 'Đến ngày',
                    value: to,
                    firstDate: from,
                    onChanged: (value) => setSheetState(() => to = value),
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: reason,
                    maxLength: 500,
                    maxLines: 2,
                    decoration: const InputDecoration(labelText: 'Lý do'),
                    validator: (v) => v == null || v.trim().isEmpty
                        ? 'Vui lòng nhập lý do.'
                        : null,
                  ),
                  const SizedBox(height: 12),
                  FilledButton(
                    onPressed: () async {
                      if (!formKey.currentState!.validate() ||
                          studentId == null) {
                        return;
                      }
                      try {
                        final fromString = _dateString(from);
                        final toString = _dateString(to);
                        if (item == null) {
                          await widget.api.request(
                            'POST',
                            '/parent/absences',
                            body: {
                              'studentId': studentId,
                              'fromDate': fromString,
                              'toDate': toString,
                              'reason': reason.text.trim(),
                            },
                          );
                        } else {
                          await widget.api.request(
                            'POST',
                            '/parent/absences/${item['id']}/replace',
                            body: {
                              'studentId': studentId,
                              'fromDate': fromString,
                              'toDate': toString,
                              'reason': reason.text.trim(),
                            },
                          );
                        }
                        if (sheetContext.mounted) {
                          Navigator.pop(sheetContext, true);
                        }
                      } on ApiException catch (e) {
                        if (sheetContext.mounted) {
                          ScaffoldMessenger.of(sheetContext)
                              .showSnackBar(SnackBar(content: Text(e.message)));
                        }
                      }
                    },
                    child: Text(item == null ? 'Gửi đăng ký' : 'Lưu cập nhật'),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
    reason.dispose();
    if (saved == true) await _load();
  }

  Future<void> _cancelAbsence(String id) async {
    try {
      await widget.api.request('POST', '/parent/absences/$id/cancel');
      await _load();
    } on ApiException catch (e) {
      if (mounted) _showMessage(e.message);
    }
  }

  DateTime? _parseDate(String? str) {
    if (str == null || str.isEmpty) return null;
    final dt = DateTime.tryParse(str);
    if (dt == null) return null;
    return DateTime(dt.year, dt.month, dt.day);
  }

  String _dateString(DateTime date) =>
      '${date.year.toString().padLeft(4, '0')}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';

  void _showMessage(String message) =>
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(message)));
}

class _DatePickerField extends StatelessWidget {
  const _DatePickerField({
    required this.label,
    required this.value,
    required this.onChanged,
    this.firstDate,
  });
  final String label;
  final DateTime value;
  final DateTime? firstDate;
  final ValueChanged<DateTime> onChanged;

  @override
  Widget build(BuildContext context) => InkWell(
    borderRadius: BorderRadius.circular(14),
    onTap: () async {
      final effectiveFirst = firstDate ?? DateTime(2020);
      final effectiveLast = DateTime(2100);
      var initial = DateTime(value.year, value.month, value.day);
      if (initial.isBefore(effectiveFirst)) initial = effectiveFirst;
      if (initial.isAfter(effectiveLast)) initial = effectiveLast;

      final chosen = await showDatePicker(
        context: context,
        initialDate: initial,
        firstDate: effectiveFirst,
        lastDate: effectiveLast,
      );
      if (chosen != null) onChanged(chosen);
    },
    child: InputDecorator(
      decoration: InputDecoration(
        labelText: label,
        suffixIcon: const Icon(Icons.calendar_month_outlined),
      ),
      child: Text(
        '${value.day.toString().padLeft(2, '0')}/${value.month.toString().padLeft(2, '0')}/${value.year}',
      ),
    ),
  );
}

class _FieldLabel extends StatelessWidget {
  const _FieldLabel(this.label);
  final String label;
  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(bottom: 8),
    child: Text(
      label,
      style: const TextStyle(color: _ink, fontWeight: FontWeight.w700),
    ),
  );
}

class _SectionTitle extends StatelessWidget {
  const _SectionTitle(this.title);
  final String title;
  @override
  Widget build(BuildContext context) => Text(
    title,
    style: const TextStyle(
      color: _ink,
      fontWeight: FontWeight.w800,
      fontSize: 17,
    ),
  );
}

class _InfoTile extends StatelessWidget {
  const _InfoTile({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.color,
    this.trailing,
    this.onTap,
  });
  final IconData icon;
  final String title;
  final String subtitle;
  final Color color;
  final Widget? trailing;
  final VoidCallback? onTap;
  @override
  Widget build(BuildContext context) => Material(
    color: Colors.white,
    borderRadius: BorderRadius.circular(18),
    child: InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(18),
      child: Padding(
        padding: const EdgeInsets.all(15),
        child: Row(
          children: [
            Container(
              width: 44,
              height: 44,
              decoration: BoxDecoration(
                color: color,
                borderRadius: BorderRadius.circular(14),
              ),
              child: Icon(icon, color: _green),
            ),
            const SizedBox(width: 13),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    title,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontWeight: FontWeight.w700,
                      color: _ink,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    subtitle,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: Color(0xFF718077),
                      fontSize: 12,
                      height: 1.35,
                    ),
                  ),
                ],
              ),
            ),
            ?trailing,
          ],
        ),
      ),
    ),
  );
}

class _ErrorCard extends StatelessWidget {
  const _ErrorCard(this.message);
  final String message;
  @override
  Widget build(BuildContext context) => Container(
    margin: const EdgeInsets.only(bottom: 14),
    padding: const EdgeInsets.all(14),
    decoration: BoxDecoration(
      color: const Color(0xFFFFE8E3),
      borderRadius: BorderRadius.circular(14),
    ),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Icon(Icons.error_outline_rounded, color: Color(0xFF9A382A)),
        const SizedBox(width: 10),
        Expanded(
          child: Text(
            message,
            style: const TextStyle(color: Color(0xFF7D3024)),
          ),
        ),
      ],
    ),
  );
}

class _EmptyState extends StatelessWidget {
  const _EmptyState({required this.title, required this.body});
  final String title;
  final String body;
  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.all(22),
    decoration: BoxDecoration(
      color: Colors.white,
      borderRadius: BorderRadius.circular(18),
    ),
    child: Column(
      children: [
        const Icon(Icons.inbox_outlined, color: _green, size: 30),
        const SizedBox(height: 8),
        Text(
          title,
          style: const TextStyle(fontWeight: FontWeight.w800, color: _ink),
        ),
        const SizedBox(height: 4),
        Text(
          body,
          textAlign: TextAlign.center,
          style: const TextStyle(color: Color(0xFF718077), fontSize: 12),
        ),
      ],
    ),
  );
}
