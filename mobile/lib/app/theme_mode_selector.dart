import 'package:flutter/material.dart';

class ThemeModeSelector extends StatelessWidget {
  const ThemeModeSelector({
    super.key,
    required this.themeMode,
    required this.onChanged,
  });

  final ThemeMode themeMode;
  final ValueChanged<ThemeMode> onChanged;

  static const _labels = {
    ThemeMode.system: 'Theo thiết bị',
    ThemeMode.light: 'Sáng',
    ThemeMode.dark: 'Tối',
  };

  @override
  Widget build(BuildContext context) {
    return ListTile(
      leading: const Icon(Icons.palette_outlined),
      title: const Text('Giao diện'),
      subtitle: Text(_labels[themeMode]!),
      trailing: DropdownButtonHideUnderline(
        child: DropdownButton<ThemeMode>(
          value: themeMode,
          alignment: AlignmentDirectional.centerEnd,
          items: [
            for (final mode in ThemeMode.values)
              DropdownMenuItem(value: mode, child: Text(_labels[mode]!)),
          ],
          onChanged: (mode) {
            if (mode != null) onChanged(mode);
          },
        ),
      ),
    );
  }
}
