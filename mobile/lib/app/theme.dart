import 'package:flutter/material.dart';

abstract final class MealTraceColors {
  static const ink = Color(0xFF26334A);
  static const muted = Color(0xFF647086);
  static const cream = Color(0xFFF0E6DB);
  static const surface = Color(0xFFF7EEE4);
  static const appBar = Color(0xFFEAE2F2);
  static const border = Color(0xFFDDD0C0);
  static const coral = Color(0xFFAD4231);
  static const coralLight = Color(0xFFFFE1D5);
  static const coralAccent = Color(0xFFED7963);
  static const violet = Color(0xFF62469B);
  static const violetLight = Color(0xFFE8DDFF);
  static const violetMuted = Color(0xFF594B77);
  static const mint = Color(0xFFBFF0DB);
  static const teal = Color(0xFF166957);
  static const error = Color(0xFF9D333B);

  static const darkInk = Color(0xFFF4F2F8);
  static const darkMuted = Color(0xFFAAA8B8);
  static const darkCanvas = Color(0xFF0F0F14);
  static const darkSurface = Color(0xFF13131A);
  static const darkAppBar = Color(0xFF13131A);
  static const darkBorder = Color(0xFF2C2C38);
  static const darkCoral = Color(0xFFC0392B);
  static const darkCoralAccent = Color(0xFFFF9A86);
  static const darkCoralContainer = Color(0xFF48231F);
  static const darkViolet = Color(0xFF8A2BE2);
  static const darkVioletContainer = Color(0xFF302142);
  static const darkMint = Color(0xFF10B981);
  static const darkMintInk = Color(0xFFA7F3D0);
  static const darkTeal = Color(0xFF15372D);
  static const darkError = Color(0xFFFF8A80);

  static const mealTile = Color(0xFFF7E0D6);
  static const mealTileInk = Color(0xFF9D4335);
  static const absenceTile = Color(0xFFEAE2F5);
  static const absenceTileInk = Color(0xFF62469B);
  static const peopleTile = Color(0xFFDDEDE3);
  static const peopleTileInk = Color(0xFF28634F);
  static const accountTile = Color(0xFFE5E4F4);
  static const accountTileInk = Color(0xFF4F5788);
  static const calendarTile = Color(0xFFF4E9C8);
  static const calendarTileInk = Color(0xFF80601E);

  static const darkMealTile = Color(0xFF2A181A);
  static const darkMealTileInk = Color(0xFFFF9A86);
  static const darkAbsenceTile = Color(0xFF302142);
  static const darkAbsenceTileInk = Color(0xFFD8C8FF);
  static const darkPeopleTile = Color(0xFF132A1E);
  static const darkPeopleTileInk = Color(0xFFA7F3D0);
  static const darkAccountTile = Color(0xFF282531);
  static const darkAccountTileInk = Color(0xFFC8CCFF);
  static const darkCalendarTile = Color(0xFF3D3829);
  static const darkCalendarTileInk = Color(0xFFFFE39B);
}

final mealTraceTheme = buildMealTraceTheme();
final mealTraceDarkTheme = buildMealTraceTheme(dark: true);

ThemeData buildMealTraceTheme({bool dark = false}) {
  final canvas = dark ? MealTraceColors.darkCanvas : MealTraceColors.cream;
  final surface = dark ? MealTraceColors.darkSurface : MealTraceColors.surface;
  final appBar = dark ? MealTraceColors.darkAppBar : MealTraceColors.appBar;
  final ink = dark ? MealTraceColors.darkInk : MealTraceColors.ink;
  final muted = dark ? MealTraceColors.darkMuted : MealTraceColors.muted;
  final border = dark ? MealTraceColors.darkBorder : MealTraceColors.border;
  final primary = dark ? MealTraceColors.darkCoral : MealTraceColors.coral;
  const onPrimary = Colors.white;
  final primaryContainer = dark
      ? MealTraceColors.darkCoralContainer
      : MealTraceColors.coralLight;
  final secondary = dark ? MealTraceColors.darkViolet : MealTraceColors.violet;
  final secondaryContainer = dark
      ? MealTraceColors.darkVioletContainer
      : MealTraceColors.violetLight;
  final tertiary = dark ? MealTraceColors.darkMint : MealTraceColors.teal;
  final tertiaryContainer = dark
      ? MealTraceColors.darkTeal
      : MealTraceColors.mint;

  final inputRadius = BorderRadius.circular(dark ? 16 : 12);

  return ThemeData(
    useMaterial3: true,
    brightness: dark ? Brightness.dark : Brightness.light,
    colorScheme:
        ColorScheme.fromSeed(
          seedColor: MealTraceColors.coral,
          brightness: dark ? Brightness.dark : Brightness.light,
        ).copyWith(
          primary: primary,
          onPrimary: dark ? Colors.white : onPrimary,
          primaryContainer: primaryContainer,
          onPrimaryContainer: dark
              ? MealTraceColors.coralLight
              : MealTraceColors.ink,
          secondary: secondary,
          onSecondary: Colors.white,
          secondaryContainer: secondaryContainer,
          onSecondaryContainer: dark
              ? MealTraceColors.darkInk
              : MealTraceColors.violetMuted,
          tertiary: tertiary,
          onTertiary: dark ? const Color(0xFF06251B) : Colors.white,
          tertiaryContainer: tertiaryContainer,
          onTertiaryContainer: dark
              ? MealTraceColors.darkMintInk
              : MealTraceColors.teal,
          surface: surface,
          onSurface: ink,
          surfaceContainerLowest: canvas,
          surfaceContainerLow: dark
              ? const Color(0xFF17171F)
              : const Color(0xFFECE3F2),
          surfaceContainer: dark
              ? const Color(0xFF1E1E28)
              : const Color(0xFFF2E8DD),
          surfaceContainerHigh: dark
              ? const Color(0xFF252530)
              : const Color(0xFFE7DBCE),
          onSurfaceVariant: muted,
          outline: dark ? const Color(0xFF454552) : const Color(0xFFD8CCBE),
          outlineVariant: border,
          error: dark ? MealTraceColors.darkError : MealTraceColors.error,
          onError: dark ? const Color(0xFF690005) : Colors.white,
        ),
    scaffoldBackgroundColor: canvas,
    appBarTheme: AppBarTheme(
      centerTitle: false,
      backgroundColor: appBar,
      foregroundColor: ink,
      iconTheme: IconThemeData(color: secondary),
      actionsIconTheme: IconThemeData(color: secondary),
      titleTextStyle: TextStyle(
        color: ink,
        fontSize: 20,
        fontWeight: FontWeight.w800,
      ),
      scrolledUnderElevation: 0,
      surfaceTintColor: Colors.transparent,
    ),
    navigationBarTheme: NavigationBarThemeData(
      height: 72,
      labelBehavior: NavigationDestinationLabelBehavior.alwaysShow,
      backgroundColor: dark ? const Color(0xFF17171F) : const Color(0xFFE8DFF0),
      indicatorColor: secondaryContainer,
      surfaceTintColor: Colors.transparent,
      iconTheme: WidgetStateProperty.resolveWith<IconThemeData?>((states) {
        final selected = states.contains(WidgetState.selected);
        return IconThemeData(color: selected ? secondary : muted);
      }),
      labelTextStyle: WidgetStateProperty.resolveWith<TextStyle?>((states) {
        final selected = states.contains(WidgetState.selected);
        return TextStyle(
          color: selected ? secondary : muted,
          fontSize: 12,
          fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
        );
      }),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        backgroundColor: primary,
        foregroundColor: onPrimary,
        minimumSize: const Size(44, 48),
        padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
        textStyle: const TextStyle(fontWeight: FontWeight.w700),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(dark ? 14 : 12),
        ),
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        foregroundColor: secondary,
        side: BorderSide(
          color: dark ? const Color(0xFF5A4B6C) : const Color(0xFFD5C8E5),
        ),
        minimumSize: const Size(44, 48),
        padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 12),
        textStyle: const TextStyle(fontWeight: FontWeight.w700),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      ),
    ),
    textButtonTheme: TextButtonThemeData(
      style: TextButton.styleFrom(
        foregroundColor: secondary,
        textStyle: const TextStyle(fontWeight: FontWeight.w700),
      ),
    ),
    cardTheme: CardThemeData(
      color: dark ? const Color(0xFF1E1E28) : surface,
      elevation: dark ? 0 : 1,
      shadowColor: dark ? Colors.transparent : const Color(0x1260503B),
      surfaceTintColor: Colors.transparent,
      margin: const EdgeInsets.symmetric(vertical: 8),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(dark ? 18 : 20),
        side: BorderSide(color: border),
      ),
    ),
    inputDecorationTheme: InputDecorationTheme(
      border: OutlineInputBorder(borderRadius: inputRadius),
      enabledBorder: OutlineInputBorder(
        borderRadius: inputRadius,
        borderSide: BorderSide(color: border),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: inputRadius,
        borderSide: BorderSide(color: secondary, width: 1.5),
      ),
      errorBorder: OutlineInputBorder(
        borderRadius: inputRadius,
        borderSide: BorderSide(
          color: dark ? MealTraceColors.darkError : MealTraceColors.error,
        ),
      ),
      focusedErrorBorder: OutlineInputBorder(
        borderRadius: inputRadius,
        borderSide: BorderSide(
          color: dark ? MealTraceColors.darkError : MealTraceColors.error,
          width: 1.5,
        ),
      ),
      filled: true,
      fillColor: dark ? const Color(0xFF1A1A23) : const Color(0xFFF1E7DC),
    ),
    bottomSheetTheme: BottomSheetThemeData(
      showDragHandle: true,
      backgroundColor: surface,
      surfaceTintColor: Colors.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
    ),
    progressIndicatorTheme: ProgressIndicatorThemeData(color: primary),
    dividerColor: border,
  );
}

ThemeData buildParentDarkTheme(ThemeData base) {
  const canvas = Color(0xFF0F0F14);
  const surface = Color(0xFF13131A);
  const card = Color(0xFF1E1E28);
  const border = Color(0xFF2C2C38);
  const violet = Color(0xFF8A2BE2);
  const green = Color(0xFF10B981);
  final scheme = base.colorScheme.copyWith(
    brightness: Brightness.dark,
    primary: const Color(0xFFC0392B),
    onPrimary: Colors.white,
    primaryContainer: const Color(0xFF48231F),
    onPrimaryContainer: const Color(0xFFFFDAD2),
    secondary: violet,
    onSecondary: Colors.white,
    secondaryContainer: const Color(0xFF302142),
    onSecondaryContainer: const Color(0xFFE2D3FF),
    tertiary: green,
    onTertiary: const Color(0xFF06251B),
    tertiaryContainer: const Color(0xFF15372D),
    onTertiaryContainer: const Color(0xFFA7F3D0),
    surface: surface,
    onSurface: const Color(0xFFF4F2F8),
    surfaceContainerLowest: canvas,
    surfaceContainerLow: const Color(0xFF17171F),
    surfaceContainer: card,
    surfaceContainerHigh: const Color(0xFF252530),
    onSurfaceVariant: const Color(0xFFAAA8B8),
    outline: const Color(0xFF454552),
    outlineVariant: border,
  );
  final inputBorder = OutlineInputBorder(
    borderRadius: BorderRadius.circular(16),
    borderSide: const BorderSide(color: border),
  );
  return base.copyWith(
    brightness: Brightness.dark,
    colorScheme: scheme,
    scaffoldBackgroundColor: canvas,
    appBarTheme: base.appBarTheme.copyWith(
      backgroundColor: surface,
      foregroundColor: scheme.onSurface,
      iconTheme: const IconThemeData(color: Color(0xFFBBA2FF)),
      actionsIconTheme: const IconThemeData(color: Color(0xFFBBA2FF)),
      titleTextStyle: base.textTheme.titleLarge?.copyWith(
        color: scheme.onSurface,
        fontWeight: FontWeight.w800,
      ),
    ),
    cardTheme: base.cardTheme.copyWith(
      color: card,
      elevation: 0,
      shadowColor: Colors.transparent,
      surfaceTintColor: Colors.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(18),
        side: const BorderSide(color: border),
      ),
    ),
    inputDecorationTheme: base.inputDecorationTheme.copyWith(
      filled: true,
      fillColor: const Color(0xFF1A1A23),
      border: inputBorder,
      enabledBorder: inputBorder,
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(16),
        borderSide: const BorderSide(color: violet, width: 1.5),
      ),
    ),
    floatingActionButtonTheme: base.floatingActionButtonTheme.copyWith(
      backgroundColor: const Color(0xFFC0392B),
      foregroundColor: Colors.white,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(18)),
    ),
  );
}
