import 'package:flutter/material.dart';

class MealAction {
  const MealAction(this.label, this.onPressed, {this.primary = false});
  final String label;
  final VoidCallback? onPressed;
  final bool primary;
}

/// Keeps a group together. Narrow layouts expose every action in one menu.
class MealActions extends StatelessWidget {
  const MealActions({
    super.key,
    required this.actions,
    this.menuLabel = 'Thao tác',
  });
  final List<MealAction> actions;
  final String menuLabel;

  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, bounds) {
      if (actions.isEmpty) return const SizedBox.shrink();
      final style = Theme.of(context).textTheme.labelLarge!;
      final widths = actions.map((action) {
        final painter = TextPainter(
          text: TextSpan(text: action.label, style: style),
          textDirection: Directionality.of(context),
          textScaler: MediaQuery.textScalerOf(context),
          maxLines: 1,
        )..layout();
        final width = painter.width + 40;
        painter.dispose();
        return width;
      }).toList();
      final requiredWidth =
          widths.fold<double>(0, (sum, width) => sum + width) +
          8 * (actions.length - 1);
      if (requiredWidth > bounds.maxWidth) {
        return PopupMenuButton<int>(
          tooltip: menuLabel,
          enabled: actions.any((action) => action.onPressed != null),
          onSelected: (index) => actions[index].onPressed?.call(),
          itemBuilder: (_) => [
            for (final (index, action) in actions.indexed)
              PopupMenuItem(
                value: index,
                enabled: action.onPressed != null,
                child: Text(action.label),
              ),
          ],
          child: Semantics(
            button: true,
            child: Container(
              constraints: const BoxConstraints(minHeight: 48),
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
              decoration: BoxDecoration(
                color: MealColors.lavender,
                borderRadius: BorderRadius.circular(14),
                border: Border.all(
                  color: MealColors.primary.withValues(alpha: .2),
                ),
              ),
              child: Row(
                children: [
                  Expanded(
                    child: Text(
                      menuLabel,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: style,
                    ),
                  ),
                  const Icon(Icons.keyboard_arrow_down),
                ],
              ),
            ),
          ),
        );
      }
      return Row(
        children: [
          for (final (index, action) in actions.indexed) ...[
            if (index > 0) const SizedBox(width: 8),
            Expanded(
              flex: (widths[index] * 100).round(),
              child: Tooltip(
                message: action.label,
                child: action.primary
                    ? FilledButton(
                        onPressed: action.onPressed,
                        child: Text(
                          action.label,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                      )
                    : OutlinedButton(
                        onPressed: action.onPressed,
                        child: Text(
                          action.label,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
              ),
            ),
          ],
        ],
      );
    },
  );
}

abstract final class MealColors {
  static const primary = Color(0xFF6D4DB1),
      ink = Color(0xFF29344A),
      muted = Color(0xFF6C768A);
  static const background = Color(0xFFF8F6F2),
      lavender = Color(0xFFF0EBFA),
      border = Color(0xFFE9E4DE);
  static const mint = Color(0xFFE8F4ED),
      green = Color(0xFF28674E),
      peach = Color(0xFFFFEEE5);
}

class MealIcon extends StatelessWidget {
  const MealIcon(
    this.icon, {
    super.key,
    this.color = MealColors.primary,
    this.background = MealColors.lavender,
    this.size = 44,
  });
  final IconData icon;
  final Color color, background;
  final double size;
  @override
  Widget build(BuildContext context) => Container(
    width: size,
    height: size,
    decoration: BoxDecoration(
      color: background,
      borderRadius: BorderRadius.circular(size * .32),
    ),
    alignment: Alignment.center,
    child: Icon(icon, color: color, size: size * .48),
  );
}

class MealHero extends StatelessWidget {
  const MealHero({
    super.key,
    required this.title,
    required this.subtitle,
    required this.icon,
    this.eyebrow,
  });
  final String title, subtitle;
  final String? eyebrow;
  final IconData icon;
  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.all(22),
    margin: const EdgeInsets.only(bottom: 24),
    decoration: BoxDecoration(
      borderRadius: BorderRadius.circular(26),
      gradient: const LinearGradient(
        begin: Alignment.topLeft,
        end: Alignment.bottomRight,
        colors: [Color(0xFFF0E9FA), Color(0xFFFAF4ED)],
      ),
      border: Border.all(color: const Color(0xFFE7DDF1)),
    ),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            MealIcon(icon, background: Colors.white.withValues(alpha: .8)),
            if (eyebrow != null) ...[
              const SizedBox(width: 12),
              Expanded(
                child: Text(
                  eyebrow!,
                  style: Theme.of(
                    context,
                  ).textTheme.labelLarge?.copyWith(color: MealColors.primary),
                ),
              ),
            ],
          ],
        ),
        const SizedBox(height: 18),
        Text(
          title,
          style: Theme.of(context).textTheme.headlineSmall?.copyWith(
            fontWeight: FontWeight.w800,
            color: MealColors.ink,
            height: 1.25,
          ),
        ),
        const SizedBox(height: 10),
        Text(
          subtitle,
          style: Theme.of(context).textTheme.bodyMedium?.copyWith(
            color: MealColors.muted,
            height: 1.5,
          ),
        ),
      ],
    ),
  );
}

class MealSectionTitle extends StatelessWidget {
  const MealSectionTitle(this.title, {super.key, this.subtitle});
  final String title;
  final String? subtitle;
  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(bottom: 14),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          title,
          style: Theme.of(
            context,
          ).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w800),
        ),
        if (subtitle != null) ...[
          const SizedBox(height: 4),
          Text(
            subtitle!,
            style: Theme.of(
              context,
            ).textTheme.bodySmall?.copyWith(color: MealColors.muted),
          ),
        ],
      ],
    ),
  );
}

class MealStatus extends StatelessWidget {
  const MealStatus(this.label, {super.key, this.positive = false});
  final String label;
  final bool positive;
  @override
  Widget build(BuildContext context) => Align(
    alignment: Alignment.centerLeft,
    child: Container(
      padding: const EdgeInsets.symmetric(horizontal: 11, vertical: 7),
      decoration: BoxDecoration(
        color: positive ? MealColors.mint : MealColors.lavender,
        borderRadius: BorderRadius.circular(10),
      ),
      child: Text(
        label,
        style: Theme.of(context).textTheme.labelMedium?.copyWith(
          color: positive ? MealColors.green : MealColors.primary,
          fontWeight: FontWeight.w700,
        ),
      ),
    ),
  );
}

class MealEmpty extends StatelessWidget {
  const MealEmpty(this.message, {super.key, this.icon = Icons.inbox_outlined});
  final String message;
  final IconData icon;
  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 32, horizontal: 16),
    child: Column(
      children: [
        MealIcon(icon, size: 60),
        const SizedBox(height: 16),
        Text(
          message,
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.bodyMedium?.copyWith(
            color: MealColors.muted,
            height: 1.5,
          ),
        ),
      ],
    ),
  );
}

class MealMetric extends StatelessWidget {
  const MealMetric({
    super.key,
    required this.label,
    required this.value,
    this.detail,
    this.icon = Icons.restaurant_outlined,
  });
  final String label, value;
  final String? detail;
  final IconData icon;
  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.all(20),
    margin: const EdgeInsets.symmetric(vertical: 12),
    decoration: BoxDecoration(
      color: MealColors.primary,
      borderRadius: BorderRadius.circular(24),
    ),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Icon(icon, color: Colors.white70, size: 22),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                label,
                style: Theme.of(
                  context,
                ).textTheme.labelLarge?.copyWith(color: Colors.white70),
              ),
            ),
          ],
        ),
        const SizedBox(height: 14),
        Text(
          value,
          style: Theme.of(context).textTheme.headlineMedium?.copyWith(
            color: Colors.white,
            fontWeight: FontWeight.w800,
          ),
        ),
        if (detail != null) ...[
          const SizedBox(height: 8),
          Text(
            detail!,
            style: Theme.of(
              context,
            ).textTheme.bodyMedium?.copyWith(color: Colors.white),
          ),
        ],
      ],
    ),
  );
}
