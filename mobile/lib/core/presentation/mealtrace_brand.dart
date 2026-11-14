import 'package:flutter/material.dart';

import 'meal_widgets.dart';

class MealTraceBrand extends StatelessWidget {
  const MealTraceBrand({super.key, this.iconSize = 42, this.fontSize = 25});

  final double iconSize;
  final double fontSize;

  @override
  Widget build(BuildContext context) {
    final dark = Theme.of(context).brightness == Brightness.dark;
    return SizedBox(
      width: 220,
      child: FittedBox(
        fit: BoxFit.scaleDown,
        alignment: Alignment.centerLeft,
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: iconSize,
              height: iconSize,
              decoration: BoxDecoration(
                color: MealColors.mint,
                borderRadius: BorderRadius.circular(iconSize * .34),
              ),
              child: Icon(
                Icons.eco_outlined,
                size: iconSize * .57,
                color: MealColors.primary,
              ),
            ),
            SizedBox(width: iconSize * .22),
            Text.rich(
              TextSpan(
                style: TextStyle(
                  fontSize: fontSize,
                  fontWeight: FontWeight.w800,
                  letterSpacing: -.6,
                  color: dark
                      ? Theme.of(context).colorScheme.onSurface
                      : MealColors.ink,
                ),
                children: [
                  const TextSpan(text: 'Meal'),
                  TextSpan(
                    text: 'Trace',
                    style: TextStyle(color: MealColors.primary),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
