---
description: Azure DevOps documentation standards and best practices
applyTo: "wiki, wiki pages, *.md,*.markdown, *.rst,*.adoc, *.asciidoc,*.txt, *."
---

# Documentation Guidelines for Azure DevOps

Follow these standards to ensure high-quality, maintainable, and user-friendly documentation across the project:

1. **Formatting & Style**
    - Adhere to the [Azure DevOps Documentation Style Guide](https://learn.microsoft.com/en-us/azure/devops/project/wiki/markdown-guidance?view=azure-devops) for all markdown and wiki content.
    - Use clear, concise, and consistent language. Avoid jargon unless necessary, and provide definitions or tooltips for complex terms.
    - Structure documents with meaningful headings, lists, and tables for readability.

2. **Visual Aids**
    - Use Mermaid diagrams to illustrate processes, workflows, and system architecture. Place diagrams near relevant content.
    - Incorporate icons for better visualization where appropriate.
    - Provide tooltips for acronyms or technical terms to aid understanding.

3. **Code & Examples**
    - Include code snippets and practical examples to clarify complex concepts or usage.
    - Use syntax highlighting for code blocks.

4. **Maintenance**
    - Keep documentation up-to-date with the latest features, changes, and best practices.
    - Regularly review and revise content for accuracy and clarity.

5. **Collaboration**
    - Encourage feedback and contributions from team members to continuously improve documentation quality.
    - Use pull requests and code reviews for documentation changes.

6. **Internal Links**
    - Encode internal navigation links as follows: `[link text](./path/to/file%2D01.md)` (use `%2D` instead of `-`).

---

## Sample Mermaid Diagram

:::mermaid
graph TD
     A["Start"] --> B{"Is it <br> working?"}
     B -- Yes --> C["Great!"]
     B -- No --> D["Fix it"]
     D --> B

     %% Node styling
     classDef start fill:#4F8A8B,color:#fff;
     classDef decision fill:#F9D342,color:#222;
     classDef success fill:#30B67B,color:#fff;
     classDef action fill:#E84545,color:#fff;

     class A start;
     class B decision;
     class C success;
     class D action;
:::
**Tip:** Use icons and tooltips and always wrap the text in double quotes like ["📥 Incoming Request"] to enhance user experience and provide additional context where needed.
___
:::mermaid
graph TD
    subgraph "🔧 Correlation ID Implementation"
        A["📥 Incoming Request"] --> B{Has X-Correlation-ID?}
        B -->|Yes| C["✅ Use Existing ID"]
        B -->|No| D["🆕 Generate New GUID"]

        C --> E["📝 Add to Serilog Context"]
        D --> E
        
        E --> F["📤 Add to Response Headers"]
        F --> G["🚀 Process Request"]
        
        G --> H{Outgoing Refit Call?}
        H -->|Yes| I["🔄 CorrelationIdHandler<br/>Auto-adds Header"]
        H -->|No| J["✅ Complete Request"]
        
        I --> K["📨 Service Call with<br/>X-Correlation-ID"]
        K --> L["🔄 Receiving Service<br/>Processes Same ID"]
        L --> J
    end
    
    %% Styling
    classDef process fill:#e8f5e8,stroke:#2e7d32,stroke-width:2px
    classDef decision fill:#fff3e0,stroke:#ef6c00,stroke-width:2px
    classDef auto fill:#f3e5f5,stroke:#7b1fa2,stroke-width:2px
    classDef complete fill:#e0f2f1,stroke:#00695c,stroke-width:2px
    
    class A,C,D,E,F,G,K,L process
    class B,H decision
    class I auto
    class J complete
:::
---
**Tip:** Use icons and tooltips to enhance user experience and provide additional context where needed.
