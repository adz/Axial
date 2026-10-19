---
title: Cache
api:
  family: Axial.CacheModule
  sections:
    - id: create
      title: Create caches
      order: 10
      members: ["make", "makeBounded", "makeBoundedWithExpiry", "makeRec"]
      facets:
        task: [create]
    - id: access
      title: Access values
      order: 20
      members: ["get", "count"]
      facets:
        task: [access]
    - id: invalidate
      title: Invalidate values
      order: 30
      members: ["invalidate", "invalidateAll"]
      facets:
        task: [invalidate]
---
# Cache
