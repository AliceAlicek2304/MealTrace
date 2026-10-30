import 'package:flutter/material.dart';

import 'theme.dart';

class MealTraceBrand extends StatelessWidget {
  const MealTraceBrand({super.key, this.iconSize = 42, this.fontSize = 25});

  final double iconSize;
  final double fontSize;

  @override
  Widget build(BuildContext context) {
    final dark = Theme.of(context).brightness == Brightness.dark;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: iconSize,
          height: iconSize,
          decoration: BoxDecoration(
            color: dark ? MealTraceColors.darkTeal : MealTraceColors.mint,
            borderRadius: BorderRadius.circular(iconSize * .34),
          ),
          child: Icon(
            Icons.eco_outlined,
            size: iconSize * .57,
            color: dark ? MealTraceColors.darkMint : MealTraceColors.teal,
          ),
        ),
        SizedBox(width: iconSize * .22),
        Text.rich(
          TextSpan(
            style: TextStyle(
              fontSize: fontSize,
              fontWeight: FontWeight.w800,
              letterSpacing: -.6,
              color: dark ? MealTraceColors.darkInk : MealTraceColors.ink,
            ),
            children: [
              const TextSpan(text: 'Meal'),
              TextSpan(
                text: 'Trace',
                style: TextStyle(
                  color: dark
                      ? MealTraceColors.darkCoralAccent
                      : MealTraceColors.coralAccent,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

