import 'package:flutter/material.dart';
import '../core/presentation/meal_widgets.dart';

final mealTraceTheme = ThemeData(
  useMaterial3: true,
  fontFamily: 'Roboto',
  colorScheme: ColorScheme.fromSeed(seedColor: MealColors.primary).copyWith(
    primary: MealColors.primary,
    onPrimary: Colors.white,
    surface: Colors.white,
    onSurface: MealColors.ink,
    onSurfaceVariant: MealColors.muted,
    outlineVariant: MealColors.border,
    primaryContainer: MealColors.lavender,
    secondaryContainer: MealColors.lavender,
    onSecondaryContainer: MealColors.primary,
  ),
  scaffoldBackgroundColor: MealColors.background,
  textTheme: ThemeData.light().textTheme
      .apply(bodyColor: MealColors.ink, displayColor: MealColors.ink)
      .copyWith(
        titleLarge: const TextStyle(
          fontFamily: 'Roboto',
          fontSize: 21,
          fontWeight: FontWeight.w800,
          height: 1.3,
          letterSpacing: -.4,
        ),
        titleMedium: const TextStyle(
          fontFamily: 'Roboto',
          fontSize: 16,
          fontWeight: FontWeight.w700,
          height: 1.4,
        ),
        titleSmall: const TextStyle(
          fontFamily: 'Roboto',
          fontSize: 14,
          fontWeight: FontWeight.w700,
          height: 1.4,
        ),
        bodyLarge: const TextStyle(
          fontFamily: 'Roboto',
          fontSize: 16,
          height: 1.5,
        ),
        bodyMedium: const TextStyle(
          fontFamily: 'Roboto',
          fontSize: 14,
          height: 1.5,
        ),
        bodySmall: const TextStyle(
          fontFamily: 'Roboto',
          fontSize: 12,
          height: 1.5,
        ),
        labelLarge: const TextStyle(
          fontFamily: 'Roboto',
          fontSize: 14,
          fontWeight: FontWeight.w700,
          height: 1.3,
        ),
      ),
  appBarTheme: const AppBarTheme(
    centerTitle: false,
    backgroundColor: MealColors.background,
    foregroundColor: MealColors.ink,
    scrolledUnderElevation: 0,
    elevation: 0,
    toolbarHeight: 68,
    titleTextStyle: TextStyle(
      fontFamily: 'Roboto',
      fontSize: 20,
      fontWeight: FontWeight.w800,
      color: MealColors.ink,
    ),
  ),
  navigationBarTheme: NavigationBarThemeData(
    height: 76,
    backgroundColor: Colors.white,
    indicatorColor: MealColors.lavender,
    elevation: 0,
    labelBehavior: NavigationDestinationLabelBehavior.alwaysShow,
    labelTextStyle: WidgetStateProperty.resolveWith(
      (states) => TextStyle(
        fontFamily: 'Roboto',
        fontSize: 11,
        fontWeight: states.contains(WidgetState.selected)
            ? FontWeight.w800
            : FontWeight.w500,
        color: states.contains(WidgetState.selected)
            ? MealColors.primary
            : MealColors.muted,
      ),
    ),
  ),
  bottomSheetTheme: const BottomSheetThemeData(
    showDragHandle: true,
    backgroundColor: Colors.white,
    dragHandleColor: MealColors.border,
    shape: RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
    ),
  ),
  filledButtonTheme: FilledButtonThemeData(
    style: FilledButton.styleFrom(
      minimumSize: const Size(48, 52),
      padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
      textStyle: const TextStyle(
        fontFamily: 'Roboto',
        fontSize: 14,
        fontWeight: FontWeight.w700,
      ),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(15)),
    ),
  ),
  outlinedButtonTheme: OutlinedButtonThemeData(
    style: OutlinedButton.styleFrom(
      minimumSize: const Size(48, 48),
      side: const BorderSide(color: Color(0xFFDED4EC)),
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(15)),
    ),
  ),
  textButtonTheme: TextButtonThemeData(
    style: TextButton.styleFrom(
      minimumSize: const Size(48, 44),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
    ),
  ),
  floatingActionButtonTheme: FloatingActionButtonThemeData(
    backgroundColor: MealColors.primary,
    foregroundColor: Colors.white,
    elevation: 3,
    focusElevation: 3,
    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(18)),
    extendedTextStyle: const TextStyle(
      fontFamily: 'Roboto',
      fontWeight: FontWeight.w700,
      fontSize: 14,
    ),
  ),
  cardTheme: CardThemeData(
    color: Colors.white,
    elevation: 0,
    margin: const EdgeInsets.symmetric(vertical: 7),
    clipBehavior: Clip.antiAlias,
    shape: RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(22),
      side: const BorderSide(color: MealColors.border),
    ),
  ),
  inputDecorationTheme: InputDecorationTheme(
    filled: true,
    fillColor: Colors.white,
    contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 17),
    labelStyle: const TextStyle(
      fontFamily: 'Roboto',
      color: MealColors.muted,
      fontSize: 14,
    ),
    hintStyle: const TextStyle(
      fontFamily: 'Roboto',
      color: MealColors.muted,
      fontSize: 14,
    ),
    border: OutlineInputBorder(
      borderRadius: BorderRadius.circular(15),
      borderSide: const BorderSide(color: MealColors.border),
    ),
    enabledBorder: OutlineInputBorder(
      borderRadius: BorderRadius.circular(15),
      borderSide: const BorderSide(color: MealColors.border),
    ),
    focusedBorder: OutlineInputBorder(
      borderRadius: BorderRadius.circular(15),
      borderSide: const BorderSide(color: MealColors.primary, width: 1.5),
    ),
  ),
  listTileTheme: const ListTileThemeData(
    contentPadding: EdgeInsets.symmetric(horizontal: 18, vertical: 6),
    iconColor: MealColors.muted,
  ),
  dividerTheme: const DividerThemeData(
    color: MealColors.border,
    thickness: 1,
    space: 1,
  ),
  expansionTileTheme: const ExpansionTileThemeData(
    shape: Border(),
    collapsedShape: Border(),
    iconColor: MealColors.primary,
    collapsedIconColor: MealColors.muted,
    tilePadding: EdgeInsets.symmetric(horizontal: 12, vertical: 3),
  ),
  dialogTheme: DialogThemeData(
    backgroundColor: Colors.white,
    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(24)),
  ),
  snackBarTheme: SnackBarThemeData(
    behavior: SnackBarBehavior.floating,
    backgroundColor: MealColors.ink,
    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
    contentTextStyle: const TextStyle(
      fontFamily: 'Roboto',
      color: Colors.white,
      fontSize: 14,
    ),
  ),
  progressIndicatorTheme: const ProgressIndicatorThemeData(
    color: MealColors.primary,
    linearTrackColor: MealColors.lavender,
  ),
);
