import 'package:flutter/material.dart';

import 'screens/login_page.dart';
import 'theme/app_colors.dart';

void main() => runApp(const MealTraceApp());

class MealTraceApp extends StatelessWidget {
  const MealTraceApp({super.key});

  @override
  Widget build(BuildContext context) => MaterialApp(
    title: 'MealTrace',
    debugShowCheckedModeBanner: false,
    theme: ThemeData(
      useMaterial3: true,
      scaffoldBackgroundColor: AppColors.paper,
      colorScheme: ColorScheme.fromSeed(seedColor: AppColors.green),
      appBarTheme: const AppBarTheme(
        backgroundColor: AppColors.paper,
        foregroundColor: AppColors.ink,
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
