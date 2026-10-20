# Prepared reference definitions

The four JSON definitions provide English/Albanian chest radiography and abdominal CT reporting structures. They contain no patient observations, demo people, organizations, staff accounts, passwords or access tokens. Albanian terminology and all structures require local radiologist review.

Nothing loads these definitions on startup, during migration scaffolding, or during a build. They are not EF `HasData` records. `scripts/Import-ReferenceTemplates.ps1` previews by default; a future explicit `-Apply` imports unpublished versions through the authorized API and skips existing name/version pairs. Publication is a separate clinically authorized action.

No import has been executed. No application database was created, migrated or seeded in this implementation session.
