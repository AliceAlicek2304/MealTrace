import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

import 'dependencies.dart';

import '../features/auth/presentation/auth_controller.dart';

import '../features/auth/presentation/login_page.dart';
import '../features/auth/presentation/account_page.dart';
import 'theme.dart';
import '../features/school/domain/school_models.dart';
import '../features/school/presentation/school_home.dart';

class MealTraceApp extends StatefulWidget {
  const MealTraceApp({super.key, this.controller, this.schoolRepository});
  final AuthController? controller;
  final SchoolRepository? schoolRepository;
  @override
  State<MealTraceApp> createState() => _MealTraceAppState();
}

class _MealTraceAppState extends State<MealTraceApp>
    with WidgetsBindingObserver {
  late final AuthController? _auth;
  SchoolRepository? _school;
  @override
  void initState() {
    super.initState();
    try {
      if (widget.controller != null) {
        _auth = widget.controller;
        _school = widget.schoolRepository;
      } else {
        final dependencies = createApplicationDependencies();
        _auth = dependencies.auth;
        _school = dependencies.school;
      }
    } on FormatException {
      _auth = null;
    }
    WidgetsBinding.instance.addObserver(this);
    _auth?.addListener(_authChanged);
    _auth?.restore();
  }

  void _authChanged() {
    if (mounted) setState(() {});
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed &&
        _auth?.status == AuthStatus.signedIn) {
      _auth?.restore();
    }
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _auth?.removeListener(_authChanged);
    if (widget.controller == null) _auth?.dispose();
    if (widget.controller == null) _school?.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final auth = _auth;
    return MaterialApp(
      key: ValueKey(auth?.status),
      title: 'MealTrace',
      locale: const Locale('vi'),
      supportedLocales: const [Locale('vi')],
      localizationsDelegates: GlobalMaterialLocalizations.delegates,
      debugShowCheckedModeBanner: false,
      theme: mealTraceTheme,
      home: auth == null
          ? const Scaffold(
              body: SafeArea(
                child: Center(
                  child: Padding(
                    padding: EdgeInsets.all(24),
                    child: Text(
                      'Ứng dụng chưa được cấu hình kết nối. Vui lòng liên hệ quản trị viên.',
                    ),
                  ),
                ),
              ),
            )
          : ListenableBuilder(
              listenable: auth,
              builder: (context, _) {
                switch (auth.status) {
                  case AuthStatus.restoring:
                    return const Scaffold(
                      body: Center(child: CircularProgressIndicator()),
                    );
                  case AuthStatus.signedOut:
                    return LoginPage(auth: auth);
                  case AuthStatus.signedIn:
                    return _school == null
                        ? AccountPage(auth: auth)
                        : SchoolHome(auth: auth, repository: _school!);
                  case AuthStatus.restoreFailed:
                    return Scaffold(
                      body: SafeArea(
                        child: Center(
                          child: Padding(
                            padding: const EdgeInsets.all(24),
                            child: Column(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                Text(
                                  auth.message ?? 'Không thể khôi phục phiên.',
                                ),
                                const SizedBox(height: 16),
                                FilledButton(
                                  onPressed: auth.busy ? null : auth.restore,
                                  child: const Text('Thử lại'),
                                ),
                                TextButton(
                                  onPressed: auth.busy ? null : auth.logout,
                                  child: const Text('Đăng xuất trên thiết bị'),
                                ),
                              ],
                            ),
                          ),
                        ),
                      ),
                    );
                }
              },
            ),
    );
  }
}
