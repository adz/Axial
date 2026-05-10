#!/usr/bin/env node

import fs from 'node:fs';
import path from 'node:path';

const repoRoot = path.resolve(path.dirname(new URL(import.meta.url).pathname), '..');
const githubBase = 'https://github.com/adz/FsFlow/blob/main';

const targets = [
  path.join(repoRoot, 'docs', 'reference'),
];

const pageSpecs = [
  {
    outPath: ['fsflow', 'flow.md'],
    title: 'Flow',
    description: 'Source-documented synchronous workflow surface in FsFlow.',
    intro:
      "This page shows the source-documented `Flow` surface: the core type, the module functions, and the `flow { }` builder.",
    sourceFiles: ['src/FsFlow/Core.fs', 'src/FsFlow/Flow.fs', 'src/FsFlow/Builders.fs'],
    sections: [
      
      {
        title: 'Core type',
        symbols: ['type:Flow'],
      },
      {
        title: 'Builder',
        symbols: ['Builders.flow'],
      },
      {
        title: 'Module functions',
        symbols: ['module:Flow', 'Flow.run', 'Flow.runFull', 'Flow.runWithToken', 'Flow.ok', 'Flow.error', 'Flow.succeed', 'Flow.value', 'Flow.fail', 'Flow.fromResult', 'Flow.fromOption', 'Flow.fromValueOption', 'Flow.orElseFlow', 'Flow.env', 'Flow.read', 'Flow.map', 'Flow.bind', 'Flow.tap', 'Flow.tapError', 'Flow.mapError', 'Flow.catch', 'Flow.orElseWith', 'Flow.orElse', 'Flow.zip', 'Flow.map2', 'Flow.map3', 'Flow.apply', 'Flow.ignore', 'Flow.localEnv', 'Flow.provideLayer', 'Flow.delay', 'Flow.traverse', 'Flow.sequence'],
      },
    ],
  },
  {
    outPath: ['fsflow', 'check.md'],
    title: 'Check',
    description: 'Source-documented pure predicate helpers for FsFlow.',
    intro:
      'This page shows the source-documented `Check` surface: the unit-failure result type and the reusable predicate helpers.',
    sourceFiles: ['src/FsFlow/Validate.fs'],
    sections: [
      {
        title: 'Core type',
        symbols: ['type:Check'],
      },
      {
        title: 'Module functions',
        symbols: ['module:Check', 'Check.fromPredicate', 'Check.not', 'Check.and', 'Check.or', 'Check.all', 'Check.any', 'Check.okIf', 'Check.failIf', 'Check.okIfSome', 'Check.okIfNone', 'Check.failIfSome', 'Check.failIfNone', 'Check.okIfValueSome', 'Check.okIfValueNone', 'Check.failIfValueSome', 'Check.failIfValueNone', 'Check.okIfNotNull', 'Check.okIfNull', 'Check.failIfNotNull', 'Check.failIfNull', 'Check.okIfNotEmpty', 'Check.okIfEmpty', 'Check.failIfNotEmpty', 'Check.failIfEmpty', 'Check.okIfEqual', 'Check.okIfNotEqual', 'Check.failIfEqual', 'Check.failIfNotEqual', 'Check.okIfNonEmptyStr', 'Check.okIfEmptyStr', 'Check.failIfNonEmptyStr', 'Check.failIfEmptyStr', 'Check.okIfNotBlank', 'Check.notBlank', 'Check.okIfBlank', 'Check.blank', 'Check.failIfNotBlank', 'Check.failIfBlank', 'Check.orError', 'Check.orErrorWith', 'Check.notNull', 'Check.notEmpty', 'Check.equal', 'Check.notEqual'],
      },
    ],
  },
  {
    outPath: ['fsflow', 'diagnostics.md'],
    title: 'Diagnostics',
    description: 'Source-documented validation diagnostics graph for FsFlow.',
    intro:
      'This page shows the source-documented `Diagnostics` surface: a tree with local error lists, `Children` branches, and merge/flatten/display helpers for reporting.',
    sourceFiles: ['src/FsFlow/Validate.fs'],
    sections: [
      {
        title: 'Graph types',
        symbols: ['type:PathSegment', 'type:Path', 'type:Diagnostic', 'type:Diagnostics'],
      },
      {
        title: 'Module functions',
        symbols: ['module:Diagnostics', 'Diagnostics.empty', 'Diagnostics.singleton', 'Diagnostics.merge', 'Diagnostics.toString', 'Diagnostics.flatten'],
      },
    ],
  },
  {
    outPath: ['fsflow', 'validation.md'],
    title: 'Validation',
    description: 'Source-documented accumulating validation for FsFlow.',
    intro:
      'This page shows the source-documented `Validation` surface: the accumulating result type, the module functions, the path-scoping helpers, and the `validate { }` builder.',
    sourceFiles: ['src/FsFlow/Validate.fs', 'src/FsFlow/Builders.fs'],
    sections: [
      
      {
        title: 'Core type',
        symbols: ['type:Validation'],
      },
      {
        title: 'Builder',
        symbols: ['src/FsFlow/Builders.fs::Builders.validate'],
      },
      {
        title: 'Module functions',
        symbols: ['module:Validation', 'Validation.toResult', 'Validation.ok', 'Validation.error', 'Validation.succeed', 'Validation.fail', 'Validation.fromResult', 'Validation.map', 'Validation.bind', 'Validation.mapError', 'Validation.map2', 'Validation.map3', 'Validation.apply', 'Validation.ignore', 'Validation.orElse', 'Validation.orElseWith', 'Validation.collect', 'Validation.sequence', 'Validation.traverseIndexed', 'Validation.merge'],
      },
      {
        title: 'Path scoping',
        symbols: ['Validation.at', 'Validation.key', 'Validation.index', 'Validation.name'],
      },
    ],
  },
  {
    outPath: ['fsflow', 'capability.md'],
    title: 'Capability',
    description: 'Source-documented capabilities and layers for FsFlow.',
    intro:
      'This page shows the source-documented capability and layer surface, including the CAPS request tokens, named capability helpers, and layer composition used for environment management in task workflows.',
    sourceFiles: ['src/FsFlow/Core.fs', 'src/FsFlow/Flow.fs'],
    sections: [
      {
        title: 'CAPS tokens',
        symbols: ['type:Needs', 'type:Env'],
      },
      {
        title: 'Capabilities',
        symbols: ['module:Capability', 'MissingCapability', 'Capability.service', 'Capability.runtime', 'Capability.environment', 'Capability.serviceFromProvider'],
      },
      {
        title: 'Layers',
        symbols: ['module:Layer', 'Layer.provideLayer'],
      },
    ],
  },
  {
    outPath: ['caps-core', 'core.md'],
    title: 'CAPS Core',
    description: 'Source-documented synchronous capability primitives for FsFlow.Caps.Core.',
    intro:
      'This page shows the source-documented `FsFlow.Caps.Core` surface: the clock, random, GUID, and environment-variable capabilities, plus the live and deterministic providers used by production and tests.',
    sourceFiles: ['src/FsFlow.Caps.Core/Core.fs'],
    sections: [
      {
        title: 'Capability types',
        symbols: ['type:IClock', 'type:IRandom', 'type:IGuid', 'type:IEnvironmentVariables', 'type:EnvironmentVariableError'],
      },
      {
        title: 'Clock',
        symbols: ['module:Clock', 'Clock.now', 'Clock.live', 'Clock.fromValue'],
      },
      {
        title: 'Random',
        symbols: ['module:Random', 'Random.nextInt', 'Random.live', 'Random.fromValue'],
      },
      {
        title: 'GUID',
        symbols: ['module:Guid', 'Guid.newGuid', 'Guid.live', 'Guid.fromValue'],
      },
      {
        title: 'Environment variables',
        symbols: ['module:EnvironmentVariables', 'EnvironmentVariables.tryGet', 'EnvironmentVariables.live', 'EnvironmentVariables.fromPairs', 'module:EnvironmentVariable', 'EnvironmentVariable.tryGet', 'EnvironmentVariable.get', 'EnvironmentVariable.getInt', 'EnvironmentVariable.getGuid', 'EnvironmentVariable.getBool', 'module:EnvironmentVariableErrors', 'EnvironmentVariableErrors.describe'],
      },
    ],
  },
  {
    outPath: ['caps-context', 'context.md'],
    title: 'CAPS Context',
    description: 'Source-documented request and execution-context primitives for FsFlow.Caps.Context.',
    intro:
      "This page shows the source-documented `FsFlow.Caps.Context` surface: the current-user model, request context record, request identifiers, locale, metadata, and request-scoped flags.",
    sourceFiles: ['src/FsFlow.Caps.Context/Context.fs'],
    sections: [
      {
        title: 'Context types',
        symbols: ['type:UserContext', 'type:RequestContext'],
      },
      {
        title: 'Current user',
        symbols: ['module:CurrentUser', 'CurrentUser.create', 'CurrentUser.fromClaimsPrincipal', 'CurrentUser.live', 'CurrentUser.hasRole', 'CurrentUser.claim'],
      },
      {
        title: 'Request context',
        symbols: ['module:RequestContext', 'RequestContext.create', 'RequestContext.live', 'RequestContext.withRequestId', 'RequestContext.withCorrelationId', 'RequestContext.withTenantId', 'RequestContext.withCurrentUser', 'RequestContext.withCulture', 'RequestContext.withMetadata', 'RequestContext.withFlags'],
      },
      {
        title: 'Request identity',
        symbols: ['module:RequestId', 'RequestId.get', 'RequestId.live', 'module:CorrelationId', 'CorrelationId.tryGet', 'CorrelationId.live', 'module:TenantId', 'TenantId.tryGet', 'TenantId.live', 'module:Locale', 'Locale.get', 'Locale.live'],
      },
      {
        title: 'Metadata and flags',
        symbols: ['module:RequestMetadata', 'RequestMetadata.empty', 'RequestMetadata.fromPairs', 'RequestMetadata.tryGet', 'RequestMetadata.contains', 'module:RequestFlags', 'RequestFlags.empty', 'RequestFlags.fromPairs', 'RequestFlags.tryGet', 'RequestFlags.isEnabled'],
      },
    ],
  },
];

function ensureDir(dirPath) {
  fs.mkdirSync(dirPath, { recursive: true });
}

function readLines(filePath) {
  return fs.readFileSync(filePath, 'utf8').split(/\r?\n/);
}

function cleanXmlDocText(text) {
  return text
    .replace(/<c>([\s\S]*?)<\/c>/gi, '`$1`')
    .replace(/<code>([\s\S]*?)<\/code>/gi, (_match, code) => {
      const trimmed = code.trim();
      return `\n\n\`\`\`fsharp\n${trimmed}\n\`\`\`\n\n`;
    })
    .replace(/<paramref name="([^"]+)"\s*\/>/gi, '`$1`')
    .replace(/<see cref="([^"]+)"\s*\/>/gi, (_match, cref) => {
      const withoutPrefix = cref.replace(/^[A-Z]:/, '');
      const lastSegment = withoutPrefix.split(/[.:]/).pop() ?? withoutPrefix;
      return `\`${lastSegment.replace(/`[0-9]+/g, '')}\``;
    })
    .trim();
}

function extractFromXml(commentLines, tag) {
  if (commentLines.length === 0) {
    return '';
  }

  const raw = commentLines
    .map((line) => line.replace(/^\s*\/\/\/\s?/, ''))
    .join('\n');

  const regex = new RegExp(`<${tag}>([\\s\\S]*?)</${tag}>`, 'i');
  const match = raw.match(regex);
  if (!match) {
    return tag === 'summary' ? cleanXmlDocText(raw.replace(/<[^>]+>[\s\S]*?<\/[^>]+>/gi, '')) : '';
  }

  const content = match[1];
  return cleanXmlDocText(content);
}

function extractParams(commentLines) {
  if (commentLines.length === 0) return [];
  const raw = commentLines.map(l => l.replace(/^\s*\/\/\/\s?/, '')).join('\n');
  const matches = [...raw.matchAll(/<param name="([^"]+)">([\s\S]*?)<\/param>/gi)];
  return matches.map(m => ({ name: m[1], description: cleanXmlDocText(m[2]) }));
}

function extractReturns(commentLines) {
  return extractFromXml(commentLines, 'returns');
}

function extractSummary(commentLines) {
  return extractFromXml(commentLines, 'summary');
}

function extractRemarks(commentLines) {
  return extractFromXml(commentLines, 'remarks');
}

function extractExamples(commentLines) {
  const raw = commentLines
    .map((line) => line.replace(/^\s*\/\/\/\s?/, ''))
    .join('\n');

  const matches = [...raw.matchAll(/<example>([\s\S]*?)<\/example>/gi)];
  return matches.map(m => cleanXmlDocText(m[1]));
}

function isAttributeLine(line) {
  return /^\s*\[<[^>]+>\]\s*$/.test(line);
}

function extractLetName(line) {
  const simple = line.match(/^\s*let\s+(?:inline\s+)?(?:private\s+)?(?:rec\s+)?(?:(``([^`]+)``)|([A-Za-z_][A-Za-z0-9_']*))(?=\s|$|[:(=<])/);
  if (simple) {
    return simple[2] || simple[3] || null;
  }

  const parenthesized = line.match(/^\s*let\s+(?:inline\s+)?(?:private\s+)?(?:rec\s+)?\(\s*(?:(``([^`]+)``)|([A-Za-z_][A-Za-z0-9_']*))\s*\)(?=\s|$|[:(=<])/);
  if (parenthesized) {
    return parenthesized[2] || parenthesized[3] || null;
  }

  return null;
}

function extractSignature(lines, startIndex) {
  let signature = '';
  for (let i = startIndex; i < lines.length; i++) {
    const line = lines[i].trim();
    if (line === '' || line.startsWith('///')) continue;
    
    // Stop at common ending characters for a signature in F#
    const endsWithAssignment = line.includes('=');
    const endsWithCe = line.includes('{');
    
    const cleanLine = lines[i].trim();
    signature += (signature ? ' ' : '') + cleanLine;
    
    if (endsWithAssignment || endsWithCe) break;
    if (i > startIndex + 10) break; // Safety break
  }
  return signature.split('=')[0].split('{')[0].trim();
}

function normalizeDocKey(value) {
  return value.toLowerCase().replace(/[^a-z0-9]/g, '');
}

function getFunctionPageName(qualifiedName) {
  return `${qualifiedName.toLowerCase().split('.').join('-')}.md`;
}

function formatMarkdownLinkTarget(targetPath) {
  return targetPath.startsWith('.') ? targetPath : `./${targetPath}`;
}

function resolveModuleOrTypePagePath(pageIndex, qualifiedName, currentPagePath, kindHint) {
  const normalizedName = normalizeDocKey(qualifiedName);
  const indexedPage = pageIndex.get(normalizedName);
  if (indexedPage) {
    return path.relative(path.dirname(currentPagePath), indexedPage).split(path.sep).join('/');
  }

  if (kindHint === 'type') {
    return getFunctionPageName(qualifiedName);
  }

  return null;
}

function extractTypeConstructors(filePath, typeLineNumber) {
  const lines = readLines(filePath);
  const typeIndex = typeLineNumber - 1;
  const typeLine = lines[typeIndex];
  if (typeLine.includes('private')) {
    return [];
  }

  const typeIndent = typeLine.match(/^ */)?.[0].length ?? 0;
  const constructors = [];

  for (let i = typeIndex + 1; i < lines.length; i++) {
    const line = lines[i];
    const trimmed = line.trim();
    const indent = line.match(/^ */)?.[0].length ?? 0;

    if (trimmed === '' || trimmed.startsWith('///') || isAttributeLine(line)) {
      continue;
    }

    if (indent <= typeIndent && !trimmed.startsWith('|')) {
      break;
    }

    if (!trimmed.startsWith('|')) {
      continue;
    }

    const constructorSource = trimmed.replace(/^\|\s*/, '');
    const constructorNameMatch = constructorSource.match(/^(?:(``([^`]+)``)|([A-Za-z_][A-Za-z0-9_']*))/);
    if (!constructorNameMatch) {
      continue;
    }

    constructors.push({
      name: constructorNameMatch[2] || constructorNameMatch[3],
      signature: constructorSource,
      line: i + 1,
    });
  }

  return constructors;
}

function extractSymbols(filePath) {
  const lines = readLines(filePath);
  const symbols = new Map();
  const moduleStack = [];
  let pendingComments = [];
  let sawExclude = false;

  const popForIndent = (indent) => {
    while (moduleStack.length && moduleStack[moduleStack.length - 1].indent >= indent) {
      moduleStack.pop();
    }
  };

  const currentPrefix = () => moduleStack.map((entry) => entry.name).join('.');

  const record = (name, kind, lineNumber, signature) => {
    if (sawExclude) {
      pendingComments = [];
      sawExclude = false;
      return;
    }

    const summary = extractSummary(pendingComments);
    const remarks = extractRemarks(pendingComments);
    const examples = extractExamples(pendingComments);
    const params = extractParams(pendingComments);
    const returns = extractReturns(pendingComments);
    const fullName = currentPrefix() ? `${currentPrefix()}.${name}` : name;
    const existing = symbols.get(fullName) ?? [];
    existing.push({
      kind,
      line: lineNumber,
      summary,
      remarks,
      examples,
      params,
      returns,
      signature,
      filePath,
    });
    symbols.set(fullName, existing);
    pendingComments = [];
    sawExclude = false;
  };

  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index];
    const trimmed = line.trim();
    const indent = line.match(/^ */)?.[0].length ?? 0;

    if (trimmed.startsWith('///')) {
      pendingComments.push(line);
      if (trimmed.includes('<exclude/>')) {
        sawExclude = true;
      }
      continue;
    }

    if (trimmed === '') {
      continue;
    }

    if (isAttributeLine(line)) {
      continue;
    }

    popForIndent(indent);

    const moduleMatch = line.match(/^(\s*)module\s+([A-Za-z_][A-Za-z0-9_']*)\s*=/);
    if (moduleMatch) {
      const moduleIndent = moduleMatch[1].length;
      popForIndent(moduleIndent);
      record(moduleMatch[2], 'module', index + 1, extractSignature(lines, index));
      moduleStack.push({ name: moduleMatch[2], indent: moduleIndent });
      continue;
    }

    const typeMatch = line.match(/^(\s*)type\s+([A-Za-z_][A-Za-z0-9_']*)/);
    if (typeMatch) {
      record(typeMatch[2], 'type', index + 1, extractSignature(lines, index));
      continue;
    }

    const letName = extractLetName(line);
    const currentModuleIndent = moduleStack.length ? moduleStack[moduleStack.length - 1].indent : -4;
    if (letName && (pendingComments.length > 0 || indent <= currentModuleIndent + 4)) {
      const name = letName;
      record(name, 'let', index + 1, extractSignature(lines, index));
      continue;
    }

    if (pendingComments.length > 0) {
      pendingComments = [];
      sawExclude = false;
    }
  }

  return symbols;
}

function buildPageIndex() {
  const pageIndex = new Map();
  for (const spec of pageSpecs) {
    const pagePath = path.resolve(repoRoot, 'docs', 'reference', ...spec.outPath);
    pageIndex.set(normalizeDocKey(spec.title), pagePath);
  }

  return pageIndex;
}

function makeSourceLink(filePath, line) {
  const relPath = path.relative(repoRoot, filePath).split(path.sep).join('/');
  return `${githubBase}/${relPath}#L${line}`;
}

function resolveSymbolDocs(symbols, qualifiedName, kindHint) {
  const docs = symbols.get(qualifiedName);
  if (!docs || docs.length === 0) {
    return [];
  }

  if (kindHint) {
    return docs.filter((doc) => doc.kind === kindHint);
  }

  if (docs.length === 1) {
    return docs;
  }

  return [docs.find((doc) => doc.kind === 'module') ?? docs.find((doc) => doc.kind === 'type') ?? docs[0]];
}

function resolveSymbolDoc(symbols, qualifiedName, kindHint) {
  return resolveSymbolDocs(symbols, qualifiedName, kindHint)[0] ?? null;
}

function renderFunctionPage(spec, symbolRef, symbolsByFile, pageIndex, currentPagePath) {
  const [sourceAlias, symbolWithKind] = symbolRef.includes('::') ? symbolRef.split('::', 2) : [null, symbolRef];
  const [kindHint, qualifiedName] = symbolWithKind.includes(':') ? symbolWithKind.split(':', 2) : [null, symbolWithKind];
  
  let doc = null;
  let docs = [];
  const searchFiles = sourceAlias ? [sourceAlias] : spec.sourceFiles;
  for (const filePath of searchFiles) {
    const fullPath = path.resolve(repoRoot, filePath);
    const symbols = symbolsByFile.get(fullPath);
    if (symbols) {
      docs = resolveSymbolDocs(symbols, qualifiedName, kindHint);
      doc = docs[0] ?? null;
      if (doc) break;
    }
  }

  if (!doc) {
    throw new Error(`Missing symbol doc for function page: ${symbolRef}`);
  }

  const shortName = qualifiedName.split('.').pop();
  const parentName = qualifiedName.includes('.') ? qualifiedName.substring(0, qualifiedName.lastIndexOf('.')) : '';
  const modulePagePath = kindHint === 'module'
    ? resolveModuleOrTypePagePath(pageIndex, qualifiedName, currentPagePath, kindHint)
    : null;
  const constructors = kindHint === 'type' ? extractTypeConstructors(doc.filePath, doc.line) : [];
  const isMultiType = kindHint === 'type' && docs.length > 1;
  const bareBuilderSignature = doc.signature && doc.signature.trim() === `let ${shortName}`;
  const showSignatureBlock = doc.signature && !(kindHint === 'type' && docs.length > 1) && !bareBuilderSignature;

  let content = `---
title: ${qualifiedName}
linkTitle: ${shortName}
---

${doc.summary || ''}

${showSignatureBlock ? `\n\`\`\`fsharp\n${doc.signature}\n\`\`\`\n` : ''}

${doc.remarks ? `## Remarks\n\n${doc.remarks}\n` : ''}

`;

  if (kindHint === 'type' && docs.length > 1) {
    content += `## Definitions\n\n`;
    for (const variant of docs) {
      content += `### \`${variant.signature}\`\n\n`;
      if (variant.summary && variant.summary !== doc.summary) {
        content += `${variant.summary}\n\n`;
      }
      if (variant.remarks && variant.remarks !== doc.remarks) {
        content += `${variant.remarks}\n\n`;
      }
      if (variant.examples && variant.examples.length > 0) {
        content += `#### Examples\n\n`;
        for (const example of variant.examples) {
          content += `${example}\n\n`;
        }
      }
      content += `- **Source**: [source](${makeSourceLink(variant.filePath, variant.line)})\n\n`;
    }
  }

  if (!isMultiType && constructors.length > 0) {
    content += `## Constructors\n\n`;
    for (const ctor of constructors) {
      content += `- \`${ctor.signature}\` [source](${makeSourceLink(doc.filePath, ctor.line)})\n`;
    }
    content += '\n';
  }

  if (doc.params && doc.params.length > 0) {
    content += `## Parameters\n\n`;
    for (const p of doc.params) {
      content += `- \`${p.name}\`: ${p.description}\n`;
    }
    content += '\n';
  }

  if (doc.returns) {
    content += `## Returns\n\n${doc.returns}\n\n`;
  }

  content += `## Information

- **Module**: ${parentName ? (modulePagePath ? `[\`${parentName}\`](${formatMarkdownLinkTarget(modulePagePath)})` : `\`${parentName}\``) : 'Global'}
- **Source**: [source](${makeSourceLink(doc.filePath, doc.line)})

`;

  if (!isMultiType && doc.examples && doc.examples.length > 0) {
    content += `## Examples\n\n`;
    for (const example of doc.examples) {
      content += `${example}\n\n`;
    }
  }

  return content;
}

function renderItem(symbols, sourcePath, symbolRef, pagePath, pageIndex) {
  const [sourceAlias, rawSymbol] = symbolRef.includes('::') ? symbolRef.split('::', 2) : [null, symbolRef];
  const symbolWithKind = sourceAlias && sourceAlias.includes(':') ? sourceAlias : rawSymbol;
  const [kindHint, qualifiedName] = symbolWithKind.includes(':') ? symbolWithKind.split(':', 2) : [null, symbolWithKind];
  const doc = resolveSymbolDoc(symbols, qualifiedName, kindHint);
  if (!doc) {
    throw new Error(`Missing symbol doc for ${symbolRef} in ${sourcePath}`);
  }

  const functionPageName = getFunctionPageName(qualifiedName);
  const targetPath =
    doc.kind === 'let'
      ? `./${functionPageName}`
      : resolveModuleOrTypePagePath(pageIndex, qualifiedName, pagePath, doc.kind);

  const label =
    doc.kind === 'let'
      ? `[\`${qualifiedName}\`](${formatMarkdownLinkTarget(functionPageName)})`
      : targetPath
        ? `${doc.kind} [\`${qualifiedName}\`](${formatMarkdownLinkTarget(targetPath)})`
        : `${doc.kind} \`${qualifiedName}\``;
    
  const summary = doc.summary ? `: ${doc.summary}` : '';
  return `- ${label}${summary} [source](${makeSourceLink(sourcePath, doc.line)})`;
}

function renderPage(spec, symbolsByFile, outPath, pageIndex) {
  let content = `---
title: ${spec.title}
---

${spec.intro}

`;

  for (const section of spec.sections) {
    content += `## ${section.title}\n\n`;
    if (section.manual) {
      content += `${section.manual}\n\n`;
    }

    for (const symbolRef of section.symbols) {
      let found = false;
      const [sourceAlias, _] = symbolRef.includes('::') ? symbolRef.split('::', 2) : [null, symbolRef];
      const searchFiles = sourceAlias ? [sourceAlias] : spec.sourceFiles;
      
      for (const filePath of searchFiles) {
        const fullPath = path.resolve(repoRoot, filePath);
        const symbols = symbolsByFile.get(fullPath);
        if (symbols) {
          try {
            const [sourceAliasInner, rawSymbol] = symbolRef.includes('::') ? symbolRef.split('::', 2) : [null, symbolRef];
            const symbolWithKind = sourceAliasInner && sourceAliasInner.includes(':') ? sourceAliasInner : rawSymbol;
            const [kindHint, qualifiedName] = symbolWithKind.includes(':') ? symbolWithKind.split(':', 2) : [null, symbolWithKind];
            const doc = resolveSymbolDoc(symbols, qualifiedName, kindHint);
            content += renderItem(symbols, fullPath, symbolRef, outPath, pageIndex) + '\n';
            if (doc && doc.kind === 'type') {
              const constructors = extractTypeConstructors(doc.filePath, doc.line);
              if (constructors.length > 0) {
                content += `### Constructors\n\n`;
                for (const ctor of constructors) {
                  content += `- \`${ctor.signature}\` [source](${makeSourceLink(doc.filePath, ctor.line)})\n`;
                }
                content += '\n';
              }
            }
            found = true;
            break;
          } catch (e) {
            // continue
          }
        }
      }
      if (!found) {
        throw new Error(`Could find doc for ${symbolRef} in any source file of page ${spec.title}`);
      }
    }
    content += '\n';
  }

  return content;
}

function generate() {
  const allSourceFiles = new Set();
  for (const spec of pageSpecs) {
    for (const file of spec.sourceFiles) {
      allSourceFiles.add(file);
    }
    for (const section of spec.sections) {
      for (const symbol of section.symbols) {
        if (symbol.includes('::')) {
          const [source, _] = symbol.split('::');
          if (!source.includes(':')) {
            allSourceFiles.add(source);
          }
        }
      }
    }
  }

  const symbolsByFile = new Map();
  for (const file of allSourceFiles) {
    symbolsByFile.set(path.resolve(repoRoot, file), extractSymbols(path.resolve(repoRoot, file)));
  }

  const pageIndex = buildPageIndex();

  for (const targetRoot of targets) {
    for (const spec of pageSpecs) {
      const outPath = path.join(targetRoot, ...spec.outPath);
      ensureDir(path.dirname(outPath));
      fs.writeFileSync(outPath, renderPage(spec, symbolsByFile, outPath, pageIndex), 'utf8');

      // Generate individual function pages
      for (const section of spec.sections) {
        for (const symbolRef of section.symbols) {
          const [sourceAlias, symbolWithKind] = symbolRef.includes('::') ? symbolRef.split('::', 2) : [null, symbolRef];
          const [kindHint, qualifiedName] = symbolWithKind.includes(':') ? symbolWithKind.split(':', 2) : [null, symbolWithKind];
          if (kindHint === 'module') continue;
          
          const functionPagePath = path.join(path.dirname(outPath), getFunctionPageName(qualifiedName));
          fs.writeFileSync(functionPagePath, renderFunctionPage(spec, symbolRef, symbolsByFile, pageIndex, outPath), 'utf8');
        }
      }
    }
  }
}

generate();
