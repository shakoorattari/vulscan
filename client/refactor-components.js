#!/usr/bin/env node
const fs = require('fs');
const path = require('path');

// Components to refactor (already done: projects, scan-history, login)
const componentsToRefactor = [
  'src/app/shared/components/layout/layout.component.ts',
  'src/app/features/scans/scans.component.ts',
  'src/app/features/scans/scan-report.component.ts',
  'src/app/features/dashboard/dashboard.component.ts',
  'src/app/features/discovery/discovery.component.ts',
  'src/app/features/packages/packages.component.ts',
  'src/app/features/reports/reports.component.ts',
  'src/app/features/reports/vulnerability-detail.component.ts',
  'src/app/features/reports/project-config.component.ts',
  'src/app/features/reports/project-detail.component.ts',
  'src/app/features/settings/settings.component.ts',
  'src/app/shared/components/project-edit-dialog.component.ts',
];

function extractTemplateAndStyles(content) {
  // Find template
  const templateMatch = content.match(/template:\s*`([\s\S]*?)`,\s*(?:styles|styleUrls?)/m);
  if (!templateMatch) {
    console.log('No inline template found');
    return null;
  }
  
  const template = templateMatch[1];
  const templateStartIndex = templateMatch.index;
  
  // Find styles
  const stylesMatch = content.match(/styles:\s*\[\s*`([\s\S]*?)`\s*\]/m);
  let styles = '';
  let stylesEndIndex = templateStartIndex;
  
  if (stylesMatch) {
    styles = stylesMatch[1];
    stylesEndIndex = stylesMatch.index + stylesMatch[0].length;
  }
  
  return {
    template: template.trim(),
    styles: styles.trim(),
    templateStartIndex,
    stylesEndIndex,
  };
}

function refactorComponent(componentPath) {
  const fullPath = path.join(__dirname, componentPath);
  
  if (!fs.existsSync(fullPath)) {
    console.log(`❌ File not found: ${componentPath}`);
    return false;
  }
  
  console.log(`\n📝 Processing: ${componentPath}`);
  
  const content = fs.readFileSync(fullPath, 'utf8');
  
  // Check if already refactored
  if (content.includes('templateUrl:') && content.includes('styleUrl:')) {
    console.log('  ✅ Already refactored');
    return true;
  }
  
  const extracted = extractTemplateAndStyles(content);
  if (!extracted) {
    console.log('  ⚠️  Could not extract template/styles');
    return false;
  }
  
  const dir = path.dirname(fullPath);
  const baseName = path.basename(fullPath, '.ts');
  const htmlPath = path.join(dir, `${baseName}.html`);
  const scssPath = path.join(dir, `${baseName}.scss`);
  
  // Write HTML file
  fs.writeFileSync(htmlPath, extracted.template, 'utf8');
  console.log(`  ✓ Created ${baseName}.html`);
  
  // Write SCSS file
  if (extracted.styles) {
    fs.writeFileSync(scssPath, extracted.styles, 'utf8');
    console.log(`  ✓ Created ${baseName}.scss`);
  }
  
  // Update TypeScript file
  let newContent = content;
  
  // Replace template with templateUrl
  newContent = newContent.replace(
    /template:\s*`[\s\S]*?`,\s*/m,
    `templateUrl: './${baseName}.html',\n  `
  );
  
  // Replace styles with styleUrl
  newContent = newContent.replace(
    /styles:\s*\[\s*`[\s\S]*?`\s*\],\s*/m,
    `styleUrl: './${baseName}.scss',\n  `
  );
  
  // Handle case where no styles exist
  if (!extracted.styles) {
    newContent = newContent.replace(
      /templateUrl: (.*?),\n/,
      `templateUrl: $1,\n  styleUrl: './${baseName}.scss',\n`
    );
    // Create empty SCSS file
    fs.writeFileSync(scssPath, '', 'utf8');
    console.log(`  ✓ Created empty ${baseName}.scss`);
  }
  
  fs.writeFileSync(fullPath, newContent, 'utf8');
  console.log(`  ✓ Updated ${baseName}.ts`);
  
  return true;
}

console.log('🚀 Starting batch component refactoring...\n');
console.log('=' .repeat(60));

let successCount = 0;
let failCount = 0;

for (const componentPath of componentsToRefactor) {
  try {
    if (refactorComponent(componentPath)) {
      successCount++;
    } else {
      failCount++;
    }
  } catch (error) {
    console.log(`  ❌ Error: ${error.message}`);
    failCount++;
  }
}

console.log('\n' + '='.repeat(60));
console.log(`\n✅ Refactored: ${successCount}`);
console.log(`❌ Failed: ${failCount}`);
console.log(`📊 Total: ${componentsToRefactor.length}`);
console.log('\n🏗️  Building application...\n');
