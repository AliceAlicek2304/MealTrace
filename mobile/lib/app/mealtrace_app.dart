import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../features/auth/presentation/auth_controller.dart';
import '../features/auth/presentation/account_page.dart';
import '../features/auth/presentation/login_page.dart';
import '../features/school/domain/school_models.dart';
import '../features/school/presentation/school_home.dart';
import 'dependencies.dart';
import 'startup_loading_screen.dart';
import 'theme.dart';

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
  late final FlutterSecureStorage? _themeStorage;
  SchoolRepository? _school;
  ThemeMode _themeMode = ThemeMode.system;
  int _themeModeRevision = 0;

  @override
  void initState() {
    super.initState();
    _themeStorage = widget.controller == null
        ? const FlutterSecureStorage()
        : null;
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
    if (_themeStorage != null) unawaited(_restoreThemeMode());
  }

  void _authChanged() {
    if (mounted) setState(() {});
  }

  Future<void> _restoreThemeMode() async {
    final revision = _themeModeRevision;
    try {
      final stored = await _themeStorage?.read(key: 'mealtrace.themeMode');
      if (!mounted || stored == null || revision != _themeModeRevision) return;
      final mode = switch (stored) {
        'light' => ThemeMode.light,
        'dark' => ThemeMode.dark,
        _ => ThemeMode.system,
      };
      setState(() => _themeMode = mode);
    } catch (_) {
      // Use the device theme if the saved preference cannot be read.
    }
  }

  void _setThemeMode(ThemeMode mode) {
    if (_themeMode == mode) return;
    _themeModeRevision++;
    setState(() => _themeMode = mode);
    unawaited(_saveThemeMode(mode));
  }

  Future<void> _saveThemeMode(ThemeMode mode) async {
    try {
      await _themeStorage?.write(
        key: 'mealtrace.themeMode',
        value: mode.name,
      );
    } catch (_) {
      // The selected theme still applies for this session.
    }
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed &&
        _auth?.status == AuthStatus.signedIn) {
      _auth?.restore(preserveSessionView: true);
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
      key: ValueKey((
        auth?.status,
        auth?.user?.id,
        auth?.user?.roles.join(','),
        auth?.user?.inspectorAccessUntil,
      )),
      title: 'MealTrace',
      locale: const Locale('vi'),
      supportedLocales: const [Locale('vi')],
      localizationsDelegates: GlobalMaterialLocalizations.delegates,
      debugShowCheckedModeBanner: false,
      theme: mealTraceTheme,
      darkTheme: mealTraceDarkTheme,
      themeMode: _themeMode,
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
                    return const StartupLoadingScreen();
                  case AuthStatus.signedOut:
                    return LoginPage(auth: auth);
                  case AuthStatus.signedIn:
                    return _school == null
                        ? AccountPage(
                            auth: auth,
                            themeMode: _themeMode,
                            onThemeModeChanged: _setThemeMode,
                          )
                        : SchoolHome(
                            auth: auth,
                            repository: _school!,
                            themeMode: _themeMode,
                            onThemeModeChanged: _setThemeMode,
                          );
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
