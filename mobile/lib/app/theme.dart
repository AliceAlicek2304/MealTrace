import 'package:flutter/material.dart';

final mealTraceTheme = ThemeData(
  useMaterial3: true,
  colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF00695C)),
  scaffoldBackgroundColor: const Color(0xFFF5F8F7),
  appBarTheme: const AppBarTheme(
    centerTitle: false,
    backgroundColor: Color(0xFFF5F8F7),
    scrolledUnderElevation: 0,
  ),
  navigationBarTheme: const NavigationBarThemeData(
    height: 72,
    labelBehavior: NavigationDestinationLabelBehavior.alwaysShow,
  ),
  bottomSheetTheme: const BottomSheetThemeData(showDragHandle: true),
  filledButtonTheme: FilledButtonThemeData(
    style: FilledButton.styleFrom(
      backgroundColor: const Color(0xFF00695C),
      foregroundColor: Colors.white,
      minimumSize: const Size(44, 48),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
    ),
  ),
  cardTheme: CardThemeData(
    color: Colors.white,
    elevation: 0,
    margin: const EdgeInsets.symmetric(vertical: 8),
    shape: RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(18),
      side: const BorderSide(color: Color(0xFFE2EBE7)),
    ),
  ),
  inputDecorationTheme: InputDecorationTheme(
    border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
    filled: true,
    fillColor: Colors.white,
  ),
);
