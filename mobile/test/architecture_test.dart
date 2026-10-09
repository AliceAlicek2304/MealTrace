import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

// Enforces source dependencies, not runtime behavior or backend authorization.
String? forbiddenDependency(String source, String target) {
  final sourceParts = source.split('/');
  final targetParts = target.split('/');
  final layer = sourceParts.length > 2 && sourceParts.first == 'features'
      ? sourceParts[2]
      : null;
  final targetLayer = targetParts.length > 2 && targetParts.first == 'features'
      ? targetParts[2]
      : null;
  final externalPackage = target.startsWith('package:');

  if (source.startsWith('core/') &&
      (target.startsWith('app/') || target.startsWith('features/'))) {
    return 'Core must not depend on app or features';
  }
  if (layer != null && target.startsWith('app/')) {
    return 'Features must not depend on the composition root';
  }
  if (layer == 'domain' &&
      (externalPackage ||
          target == 'dart:ui' ||
          target == 'dart:io' ||
          targetLayer == 'data' ||
          targetLayer == 'presentation')) {
    return 'Domain must remain independent of UI, transport and storage';
  }
  if (layer == 'data' && targetLayer == 'presentation') {
    return 'Data must not depend on presentation';
  }
  if (layer == 'presentation' &&
      (targetLayer == 'data' ||
          target.startsWith('package:http/') ||
          target.startsWith('package:flutter_secure_storage/') ||
          target == 'dart:io' ||
          target == 'dart:convert')) {
    return 'Presentation must access data through domain contracts';
  }
  return null;
}

void main() {
  test('Layer rules reject UI, transport and composition dependencies', () {
    for (final target in [
      'package:flutter/material.dart',
      'package:http/http.dart',
      'features/auth/data/auth_api.dart',
      'features/auth/presentation/auth_controller.dart',
    ]) {
      expect(
        forbiddenDependency('features/auth/domain/model.dart', target),
        isNotNull,
        reason: target,
      );
    }
    expect(
      forbiddenDependency(
        'features/auth/data/api.dart',
        'features/auth/presentation/page.dart',
      ),
      isNotNull,
    );
    expect(
      forbiddenDependency(
        'features/auth/presentation/page.dart',
        'features/other/data/api.dart',
      ),
      isNotNull,
    );
    expect(
      forbiddenDependency('core/config.dart', 'app/dependencies.dart'),
      isNotNull,
    );
    expect(
      forbiddenDependency(
        'features/auth/data/api.dart',
        'app/dependencies.dart',
      ),
      isNotNull,
    );
  });

  test('Layer rules allow contracts and dependency assembly', () {
    expect(
      forbiddenDependency(
        'features/auth/presentation/controller.dart',
        'features/auth/domain/repository.dart',
      ),
      isNull,
    );
    expect(
      forbiddenDependency(
        'features/auth/data/repository.dart',
        'features/auth/domain/repository.dart',
      ),
      isNull,
    );
    expect(
      forbiddenDependency(
        'app/dependencies.dart',
        'features/auth/data/repository.dart',
      ),
      isNull,
    );
  });

  test('All mobile source imports and exports respect layer boundaries', () {
    final root = Directory('lib').absolute.uri;
    final directives = RegExp(
      r'''^\s*(?:import|export|part)\s+([^;]+);''',
      multiLine: true,
    );
    final quotedUris = RegExp(r'''['"]([^'"]+)['"]''');
    final violations = <String>[];
    for (final file in Directory(
      'lib',
    ).listSync(recursive: true).whereType<File>()) {
      if (!file.path.endsWith('.dart')) continue;
      final source = file.absolute.uri.path.substring(root.path.length);
      for (final directive in directives.allMatches(file.readAsStringSync())) {
        for (final match in quotedUris.allMatches(directive.group(1)!)) {
          final dependency = match.group(1)!;
          final uri = Uri.parse(dependency);
          final String target;
          if (dependency.startsWith('package:mealtrace_mobile/')) {
            target = dependency.substring('package:mealtrace_mobile/'.length);
          } else if (!uri.hasScheme) {
            target = file.absolute.uri
                .resolveUri(uri)
                .path
                .substring(root.path.length);
          } else {
            target = dependency;
          }
          final reason = forbiddenDependency(source, target);
          if (reason != null) violations.add('$source → $target: $reason');
        }
      }
    }
    expect(violations, isEmpty, reason: violations.join('\n'));
  });
}
