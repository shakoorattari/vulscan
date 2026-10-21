import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { parseMarkdown } from './markdown-lite';

/**
 * Renders advisory text (Markdown from GitHub/OSV, or plain text) without ever creating HTML from it: the text is
 * parsed into plain blocks and shown with ordinary bindings, and only absolute http(s) links become links.
 */
@Component({
  selector: 'app-markdown-view',
  standalone: true,
  imports: [NgTemplateOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ng-template #inline let-items>
      @for (t of items; track $index) {
        @if (t.kind === 'text') {
          <span>{{ t.text }}</span>
        } @else if (t.kind === 'code') {
          <code>{{ t.text }}</code>
        } @else if (t.kind === 'bold') {
          <strong>{{ t.text }}</strong>
        } @else if (t.kind === 'link') {
          <a [href]="t.href" target="_blank" rel="noopener noreferrer">{{ t.text }}</a>
        }
      }
    </ng-template>

    @for (b of blocks(); track $index) {
      @if (b.kind === 'heading') {
        <p class="heading" [class]="'level-' + b.level">
          <ng-container *ngTemplateOutlet="inline; context: { $implicit: b.inlines }" />
        </p>
      } @else if (b.kind === 'paragraph') {
        <p><ng-container *ngTemplateOutlet="inline; context: { $implicit: b.inlines }" /></p>
      } @else if (b.kind === 'list') {
        @if (b.ordered) {
          <ol>
            @for (item of b.items; track $index) {
              <li><ng-container *ngTemplateOutlet="inline; context: { $implicit: item }" /></li>
            }
          </ol>
        } @else {
          <ul>
            @for (item of b.items; track $index) {
              <li><ng-container *ngTemplateOutlet="inline; context: { $implicit: item }" /></li>
            }
          </ul>
        }
      } @else if (b.kind === 'quote') {
        <blockquote><ng-container *ngTemplateOutlet="inline; context: { $implicit: b.inlines }" /></blockquote>
      } @else if (b.kind === 'code') {
        <pre>{{ b.text }}</pre>
      }
    }
  `,
  styles: `
    :host { display: block; line-height: 1.6; font-size: 14px; color: var(--neutral-900); overflow-wrap: anywhere; }
    p { margin: 0 0 12px; }
    p:last-child { margin-bottom: 0; }
    .heading { font-weight: 600; margin: 18px 0 8px; }
    .heading:first-child { margin-top: 0; }
    .level-1 { font-size: 18px; } .level-2 { font-size: 16px; } .level-3, .level-4, .level-5, .level-6 { font-size: 14px; }
    ul, ol { margin: 0 0 12px; padding-left: 22px; }
    li { margin: 2px 0; }
    code {
      font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: 12.5px;
      padding: 1px 5px; border-radius: 4px; background: var(--neutral-200);
    }
    pre {
      margin: 0 0 12px; padding: 12px 14px; overflow-x: auto; border-radius: var(--radius-sm);
      background: var(--neutral-100); border: 1px solid var(--neutral-200);
      font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: 12.5px; line-height: 1.5;
    }
    blockquote { margin: 0 0 12px; padding: 4px 14px; border-left: 3px solid var(--neutral-300); color: var(--neutral-700); }
    a { color: var(--brand-teal-700); }
  `,
})
export class MarkdownViewComponent {
  readonly text = input<string | null | undefined>('');
  readonly blocks = computed(() => parseMarkdown(this.text()));
}
