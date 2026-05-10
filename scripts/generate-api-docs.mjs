#!/usr/bin/env node

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, '..');
const githubBase = 'https://github.com/adz/FsFlow/blob/main';

const targets = [
  path.join(repoRoot, 'docs', 'reference'),
];

const pageSpecs = [
  {
    outPath: ['flow', '_index.md'],
    title: 'Flow',
    description: 'Source-documented workflow surface in FsFlow.',
    intro:
      "This page shows the source-documented `Flow` surface: the core type and module functions.",
    symbols: [
      { section: 'Core type', ids: ['T:FsFlow.Flow`3'] },
      { section: 'Module functions', ids: ['M:FsFlow.Flow.run', 'M:FsFlow.Flow.ok', 'M:FsFlow.Flow.error', 'M:FsFlow.Flow.succeed', 'M:FsFlow.Flow.value', 'M:FsFlow.Flow.fail', 'M:FsFlow.Flow.fromResult', 'M:FsFlow.Flow.fromOption', 'M:FsFlow.Flow.fromValueOption', 'M:FsFlow.Flow.orElseFlow', 'M:FsFlow.Flow.env', 'M:FsFlow.Flow.read', 'M:FsFlow.Flow.map', 'M:FsFlow.Flow.bind', 'M:FsFlow.Flow.tap', 'M:FsFlow.Flow.tapError', 'M:FsFlow.Flow.mapError', 'M:FsFlow.Flow.catch', 'M:FsFlow.Flow.orElseWith', 'M:FsFlow.Flow.orElse', 'M:FsFlow.Flow.zip', 'M:FsFlow.Flow.map2', 'M:FsFlow.Flow.map3', 'M:FsFlow.Flow.apply', 'M:FsFlow.Flow.ignore', 'M:FsFlow.Flow.localEnv', 'M:FsFlow.Flow.provideLayer', 'M:FsFlow.Flow.delay', 'M:FsFlow.Flow.traverse', 'M:FsFlow.Flow.sequence'] },
      { section: 'Concurrency', ids: ['T:FsFlow.Fiber`2', 'M:FsFlow.Flow.fork', 'M:FsFlow.Flow.join', 'M:FsFlow.Flow.interrupt'] },
      { section: 'Parallel orchestration', ids: ['M:FsFlow.Flow.zipPar', 'M:FsFlow.Flow.race'] },
      { section: 'Scheduling', ids: ['T:FsFlow.Schedule`3', 'M:FsFlow.Schedule.recurs', 'M:FsFlow.Schedule.spaced', 'M:FsFlow.Schedule.exponential', 'M:FsFlow.Schedule.jittered', 'M:FsFlow.FlowScheduleExtensions.Flow`3.Retry.Static', 'M:FsFlow.FlowScheduleExtensions.Flow`3.Repeat.Static'] },
    ],
  },
  {
    outPath: ['flow', 'builders-flow.md'],
    title: 'flow { }',
    description: 'Documentation for the flow { } computation expression.',
    intro: 'The `flow { }` builder is the primary entry point for orchestrating synchronous, async, and task-based work.',
    symbols: [
      { section: 'Builder', ids: ['P:FsFlow.Builders.flow'] },
    ]
  },
  {
    outPath: ['check', '_index.md'],
    title: 'Check',
    description: 'Source-documented pure predicate helpers for FsFlow.',
    intro:
      'This page shows the source-documented `Check` surface: the unit-failure result type and reusable predicate helpers.',
    symbols: [
      { section: 'Core type', ids: ['T:FsFlow.Check`1'] },
      { section: 'Module functions', ids: ['M:FsFlow.Check.fromPredicate', 'M:FsFlow.Check.not', 'M:FsFlow.Check.and', 'M:FsFlow.Check.or', 'M:FsFlow.Check.all', 'M:FsFlow.Check.any', 'M:FsFlow.Check.okIf', 'M:FsFlow.Check.failIf', 'M:FsFlow.Check.okIfSome', 'M:FsFlow.Check.okIfNone', 'M:FsFlow.Check.failIfSome', 'M:FsFlow.Check.failIfNone', 'M:FsFlow.Check.okIfValueSome', 'M:FsFlow.Check.okIfValueNone', 'M:FsFlow.Check.failIfValueSome', 'M:FsFlow.Check.failIfValueNone', 'M:FsFlow.Check.okIfNotNull', 'M:FsFlow.Check.okIfNull', 'M:FsFlow.Check.failIfNotNull', 'M:FsFlow.Check.failIfNull', 'M:FsFlow.Check.okIfNotEmpty', 'M:FsFlow.Check.okIfEmpty', 'M:FsFlow.Check.failIfNotEmpty', 'M:FsFlow.Check.failIfEmpty', 'M:FsFlow.Check.okIfEqual', 'M:FsFlow.Check.okIfNotEqual', 'M:FsFlow.Check.failIfEqual', 'M:FsFlow.Check.failIfNotEqual', 'M:FsFlow.Check.okIfNonEmptyStr', 'M:FsFlow.Check.okIfEmptyStr', 'M:FsFlow.Check.failIfNonEmptyStr', 'M:FsFlow.Check.failIfEmptyStr', 'M:FsFlow.Check.okIfNotBlank', 'M:FsFlow.Check.notBlank', 'M:FsFlow.Check.okIfBlank', 'M:FsFlow.Check.blank', 'M:FsFlow.Check.failIfNotBlank', 'M:FsFlow.Check.failIfBlank', 'M:FsFlow.Check.orError', 'M:FsFlow.Check.orErrorWith', 'M:FsFlow.Check.notNull', 'M:FsFlow.Check.notEmpty', 'M:FsFlow.Check.equal', 'M:FsFlow.Check.notEqual'] },
    ],
  },
  {
    outPath: ['validation', '_index.md'],
    title: 'Validation',
    description: 'Source-documented accumulating validation for FsFlow.',
    intro:
      'This page shows the source-documented `Validation` surface: the accumulating result type, module functions, and path-scoping helpers.',
    symbols: [
      { section: 'Core type', ids: ['T:FsFlow.Validation`2'] },
      { section: 'Module functions', ids: ['M:FsFlow.Validation.toResult', 'M:FsFlow.Validation.ok', 'M:FsFlow.Validation.error', 'M:FsFlow.Validation.succeed', 'M:FsFlow.Validation.fail', 'M:FsFlow.Validation.fromResult', 'M:FsFlow.Validation.map', 'M:FsFlow.Validation.bind', 'M:FsFlow.Validation.mapError', 'M:FsFlow.Validation.map2', 'M:FsFlow.Validation.map3', 'M:FsFlow.Validation.apply', 'M:FsFlow.Validation.ignore', 'M:FsFlow.Validation.orElse', 'M:FsFlow.Validation.orElseWith', 'M:FsFlow.Validation.collect', 'M:FsFlow.Validation.sequence', 'M:FsFlow.Validation.traverseIndexed', 'M:FsFlow.Validation.merge'] },
      { section: 'Path scoping', ids: ['M:FsFlow.Validation.at', 'M:FsFlow.Validation.key', 'M:FsFlow.Validation.index', 'M:FsFlow.Validation.name'] },
    ],
  },
  {
    outPath: ['validation', 'builders-validate.md'],
    title: 'validate { }',
    description: 'Documentation for the validate { } computation expression.',
    intro: 'The `validate { }` builder is used for accumulating sibling failures into a structured diagnostics graph.',
    symbols: [
      { section: 'Builder', ids: ['P:FsFlow.Builders.validate'] },
    ]
  },
  {
    outPath: ['result', '_index.md'],
    title: 'Result Builder',
    description: 'Documentation for the result { } computation expression.',
    intro: 'The `result { }` builder provides a fail-fast computation expression for standard F# Result values.',
    symbols: [
      { section: 'Builder', ids: ['P:FsFlow.Builders.result'] },
    ],
    // Add compatibility alias for old guides
    alias: 'builders-result.md'
  },
  {
    outPath: ['diagnostics', '_index.md'],
    title: 'Diagnostics',
    description: 'Source-documented validation diagnostics graph for FsFlow.',
    intro: 'The `Diagnostics` type represents a structured graph of validation failures.',
    symbols: [
      { section: 'Graph types', ids: ['T:FsFlow.PathSegment', 'T:FsFlow.Path', 'T:FsFlow.Diagnostic`1', 'T:FsFlow.Diagnostics`1'] },
      { section: 'Module functions', ids: ['M:FsFlow.Diagnostics.empty', 'M:FsFlow.Diagnostics.singleton', 'M:FsFlow.Diagnostics.merge', 'M:FsFlow.Diagnostics.toString', 'M:FsFlow.Diagnostics.flatten'] },
    ],
  },
  {
    outPath: ['capability', '_index.md'],
    title: 'Capability',
    description: 'Source-documented capabilities and layers for FsFlow.',
    intro:
      'This page shows the source-documented capability and layer surface, including CAPS request tokens and environment management helpers.',
    symbols: [
      { section: 'CAPS tokens', ids: ['T:FsFlow.Needs`1', 'T:FsFlow.Env`1', 'T:FsFlow.Env`2'] },
      { section: 'Capabilities', ids: ['T:FsFlow.MissingCapability', 'M:FsFlow.Capability.service', 'M:FsFlow.Capability.runtime', 'M:FsFlow.Capability.environment', 'M:FsFlow.Capability.serviceFromProvider'] },
      { section: 'Layers', ids: ['M:FsFlow.Layer.provideLayer'] },
    ],
  },
  {
    outPath: ['caps-core', '_index.md'],
    title: 'CAPS Core',
    description: 'Source-documented synchronous capability primitives for FsFlow.Caps.Core.',
    intro:
      '`FsFlow.Caps.Core` is the smallest shared capability package in the FsFlow CAPS story. It keeps the surface synchronous and explicit: clock, random, GUID, and environment-variable capabilities.',
    symbols: [
      { section: 'Capability types', ids: ['T:FsFlow.Caps.Core.IClock', 'T:FsFlow.Caps.Core.IRandom', 'T:FsFlow.Caps.Core.IGuid', 'T:FsFlow.Caps.Core.IEnvironmentVariables', 'T:FsFlow.Caps.Core.EnvironmentVariableError'] },
      { section: 'Clock', ids: ['M:FsFlow.Caps.Core.Clock.now', 'M:FsFlow.Caps.Core.Clock.live', 'M:FsFlow.Caps.Core.Clock.fromValue'] },
      { section: 'Random', ids: ['M:FsFlow.Caps.Core.Random.nextInt', 'M:FsFlow.Caps.Core.Random.live', 'M:FsFlow.Caps.Core.Random.fromValue'] },
      { section: 'GUID', ids: ['M:FsFlow.Caps.Core.Guid.newGuid', 'M:FsFlow.Caps.Core.Guid.live', 'M:FsFlow.Caps.Core.Guid.fromValue'] },
      { section: 'Environment variables', ids: ['M:FsFlow.Caps.Core.EnvironmentVariables.tryGet', 'M:FsFlow.Caps.Core.EnvironmentVariables.live', 'M:FsFlow.Caps.Core.EnvironmentVariables.fromPairs', 'M:FsFlow.Caps.Core.EnvironmentVariable.tryGet', 'M:FsFlow.Caps.Core.EnvironmentVariable.get', 'M:FsFlow.Caps.Core.EnvironmentVariable.getInt', 'M:FsFlow.Caps.Core.EnvironmentVariable.getGuid', 'M:FsFlow.Caps.Core.EnvironmentVariable.getBool', 'M:FsFlow.Caps.Core.EnvironmentVariableErrors.describe'] },
    ],
  },
  {
    outPath: ['caps-console', '_index.md'],
    title: 'CAPS Console',
    description: 'Source-documented console I/O capability for FsFlow.Caps.Console.',
    intro: 'This page shows the source-documented `FsFlow.Caps.Console` surface: the console interface and its helpers.',
    symbols: [
      { section: 'Capability', ids: ['T:FsFlow.Caps.Console.IConsole'] },
      { section: 'Helpers', ids: ['M:FsFlow.Caps.Console.Console.readLine', 'M:FsFlow.Caps.Console.Console.writeLine', 'M:FsFlow.Caps.Console.Console.live'] },
    ],
  },
  {
    outPath: ['caps-filesystem', '_index.md'],
    title: 'CAPS FileSystem',
    description: 'Source-documented file system capability for FsFlow.Caps.FileSystem.',
    intro: 'This page shows the source-documented `FsFlow.Caps.FileSystem` surface: the file system interface and its helpers.',
    symbols: [
      { section: 'Capability', ids: ['T:FsFlow.Caps.FileSystem.IFileSystem'] },
      { section: 'Helpers', ids: ['M:FsFlow.Caps.FileSystem.FileSystem.readAllText', 'M:FsFlow.Caps.FileSystem.FileSystem.writeAllText', 'M:FsFlow.Caps.FileSystem.FileSystem.exists', 'M:FsFlow.Caps.FileSystem.FileSystem.live'] },
    ],
  },
  {
    outPath: ['caps-http', '_index.md'],
    title: 'CAPS Http',
    description: 'Source-documented HTTP client capability for FsFlow.Caps.Http.',
    intro: 'This page shows the source-documented `FsFlow.Caps.Http` surface: the HTTP interface and its helpers.',
    symbols: [
      { section: 'Capability', ids: ['T:FsFlow.Caps.Http.IHttp'] },
      { section: 'Helpers', ids: ['M:FsFlow.Caps.Http.Http.getString', 'M:FsFlow.Caps.Http.Http.live'] },
    ],
  },
  {
    outPath: ['caps-process', '_index.md'],
    title: 'CAPS Process',
    description: 'Source-documented external process capability for FsFlow.Caps.Process.',
    intro: 'This page shows the source-documented `FsFlow.Caps.Process` surface: the process runner interface and its helpers.',
    symbols: [
      { section: 'Capability', ids: ['T:FsFlow.Caps.Process.IProcess', 'T:FsFlow.Caps.Process.ProcessResult'] },
      { section: 'Helpers', ids: ['M:FsFlow.Caps.Process.Process.execute', 'M:FsFlow.Caps.Process.Process.live'] },
    ],
  },
  {
    outPath: ['hosting', '_index.md'],
    title: 'Hosting',
    description: 'Source-documented .NET host integration for FsFlow.Hosting.',
    intro: 'This page shows the source-documented `FsFlow.Hosting` surface: the IServiceProvider adapters and startup validation.',
    symbols: [
      { section: 'Startup', ids: ['M:FsFlow.Hosting.Startup.validateEnvironment'] },
    ],
  },
  {
    outPath: ['telemetry', '_index.md'],
    title: 'Telemetry',
    description: 'Source-documented observability integration for FsFlow.Runtime.Telemetry.',
    intro: 'This page shows the source-documented `FsFlow.Runtime.Telemetry` surface: Activity.trace integration.',
    symbols: [
      { section: 'Tracing', ids: ['M:FsFlow.Runtime.Telemetry.Activity.trace'] },
    ],
  },
];

function cleanXmlDocText(text) {
  if (!text) return '';
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

function parseXmlDocs() {
  const docs = new Map();
  const xmlFiles = [];
  
  const walk = (dir) => {
    if (!fs.existsSync(dir)) return;
    const files = fs.readdirSync(dir);
    for (const file of files) {
      const fullPath = path.join(dir, file);
      if (fs.statSync(fullPath).isDirectory()) {
        walk(fullPath);
      } else if (file.endsWith('.xml') && (fullPath.includes('debug_net8.0') || fullPath.includes('debug_netstandard2.1'))) {
        xmlFiles.push(fullPath);
      }
    }
  };

  walk(path.join(repoRoot, 'artifacts', 'bin'));

  for (const xmlFile of xmlFiles) {
    const content = fs.readFileSync(xmlFile, 'utf8');
    const memberMatches = content.matchAll(/<member name=\"([^\"]+)\">([\s\S]*?)<\/member>/g);
    for (const match of memberMatches) {
      const name = match[1];
      const inner = match[2];
      
      const summary = cleanXmlDocText(inner.match(/<summary>([\s\S]*?)<\/summary>/)?.[1]);
      const remarks = cleanXmlDocText(inner.match(/<remarks>([\s\S]*?)<\/remarks>/)?.[1]);
      const returns = cleanXmlDocText(inner.match(/<returns>([\s\S]*?)<\/returns>/)?.[1]);
      
      const params = [];
      const paramMatches = inner.matchAll(/<param name=\"([^\"]+)\">([\s\S]*?)<\/param>/g);
      for (const p of paramMatches) {
        params.push({ name: p[1], description: cleanXmlDocText(p[2]) });
      }

      const examples = [];
      const exampleMatches = inner.matchAll(/<example>([\s\S]*?)<\/example>/g);
      for (const e of exampleMatches) {
        examples.push(cleanXmlDocText(e[1]));
      }

      docs.set(name, { summary, remarks, returns, params, examples });
    }
  }
  return docs;
}

function getShortName(id) {
  const parts = id.replace(/^[A-Z]:/, '').split('(')[0].split('.');
  let last = parts.pop();
  if (last.startsWith('get_')) last = last.substring(4);
  return last.replace(/`[0-9]+/g, '');
}

function getQualifiedName(id) {
  let name = id.replace(/^[A-Z]:/, '').split('(')[0];
  if (name.includes('.get_')) name = name.replace('.get_', '.');
  // Remove backticks and type parameter counts (e.g. `1, ``2)
  name = name.replace(/`+[0-9]*/g, '');
  // Fix F# Module/Extensions suffixes in the display name
  name = name.replace(/Module$/, '');
  name = name.replace(/Extensions$/, '');
  return name;
}

function getPageName(id) {
  let name = getQualifiedName(id);
  const kind = id.split(':')[0].toLowerCase();
  // Strip common namespace prefixes for cleaner filenames
  name = name.replace(/^FsFlow\.(Caps\.)?/, '');
  return `${kind}-${name.toLowerCase().split('.').join('-')}.md`;
}

function renderSymbolPage(id, doc) {
  const shortName = getShortName(id);
  const qualifiedName = getQualifiedName(id);
  
  let content = `---
title: "${qualifiedName}"
linkTitle: "${shortName}"
---

${doc.summary || ''}

${doc.remarks ? `## Remarks\n\n${doc.remarks}\n` : ''}

`;

  if (doc.params.length > 0) {
    content += `## Parameters\n\n`;
    for (const p of doc.params) {
      content += `- \`${p.name}\`: ${p.description}\n`;
    }
    content += '\n';
  }

  if (doc.returns) {
    content += `## Returns\n\n${doc.returns}\n\n`;
  }

  if (doc.examples.length > 0) {
    content += `## Examples\n\n`;
    for (const example of doc.examples) {
      content += `${example}\n\n`;
    }
  }

  return content;
}

function generate() {
  const allDocs = parseXmlDocs();
  
  for (const targetRoot of targets) {
    for (const spec of pageSpecs) {
      const outPath = path.join(targetRoot, ...spec.outPath);
      fs.mkdirSync(path.dirname(outPath), { recursive: true });
      
      let pageContent = `---
title: "${spec.title}"
---

${spec.intro}

`;

      for (const section of spec.symbols) {
        pageContent += `## ${section.section}\n\n`;
        for (const id of section.ids) {
          // Exact match or prefix match (for overloaded methods)
          let matchId = null;
          
          const typePrefix = id.split(':')[0];
          const namePart = id.split(':')[1];
          const parts = namePart.split('.');
          
          const prefixes = [typePrefix];
          if (typePrefix === 'M') prefixes.push('P');
          if (typePrefix === 'P') prefixes.push('M');

          const candidates = [];
          for (const pref of prefixes) {
            candidates.push(`${pref}:${namePart}`);
            if (parts.length >= 2) {
               const base = parts.slice(0, -1).join('.');
               const last = parts[parts.length - 1];
               candidates.push(`${pref}:${base}Module.${last}`);
               candidates.push(`${pref}:${base}Extensions.${last}`);
               candidates.push(`${pref}:${base}Builders.${last}`);
               candidates.push(`${pref}:${base}.get_${last}`);
               candidates.push(`${pref}:${base}Module.get_${last}`);
            }
          }

          for (const cand of candidates) {
            if (allDocs.has(cand)) {
              matchId = cand;
              break;
            }
            for (const key of allDocs.keys()) {
              if (key.startsWith(cand + '(') || key.startsWith(cand + '`')) {
                matchId = key;
                break;
              }
            }
            if (matchId) break;
          }

          const doc = matchId ? allDocs.get(matchId) : null;
          if (doc) {
            const pageName = getPageName(matchId);
            const qualifiedName = getQualifiedName(matchId);
            // Links are relative to the current outPath
            pageContent += `- [\`${qualifiedName}\`](./${pageName}): ${doc.summary || ''}\n`;
            
            const symbolPagePath = path.join(path.dirname(outPath), pageName);
            fs.writeFileSync(symbolPagePath, renderSymbolPage(matchId, doc), 'utf8');
          } else {
            console.warn(`Warning: Missing doc for ${id}`);
            pageContent += `- \`${getQualifiedName(id)}\` (undocumented)\n`;
          }
        }
        pageContent += '\n';
      }
      
      fs.writeFileSync(outPath, pageContent, 'utf8');
      
      // Handle alias if defined (for relref compatibility)
      if (spec.alias) {
          const aliasPath = path.join(path.dirname(outPath), spec.alias);
          fs.writeFileSync(aliasPath, pageContent, 'utf8');
      }
    }
  }
}

generate();
