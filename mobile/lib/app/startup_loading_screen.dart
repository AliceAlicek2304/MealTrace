import 'package:flutter/material.dart';

import 'mealtrace_brand.dart';

class StartupLoadingScreen extends StatelessWidget {
  const StartupLoadingScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;

    return Scaffold(
      body: SafeArea(
        child: Center(
          child: Padding(
            padding: const EdgeInsets.all(32),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const MealTraceBrand(iconSize: 68, fontSize: 31),
                const SizedBox(height: 36),
                SizedBox(
                  width: 28,
                  height: 28,
                  child: CircularProgressIndicator(
                    strokeWidth: 3,
                    color: colors.primary,
                  ),
                ),
                const SizedBox(height: 14),
                Semantics(
                  liveRegion: true,
                  child: const Text('Đang kiểm tra phiên đăng nhập…'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
