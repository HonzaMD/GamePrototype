# Pořadí v jednom snímku (Unity player loop)

```
┌─ TimeUpdate ──────────────────────────────────────────┐
│  čeká na present min. snímku, změří čas               │
│  nastaví Time.time / deltaTime / frameCount           │
│  → platí konstantně po celý zbytek snímku             │
└───────────────────────────────────────────────────────┘
┌─ EarlyUpdate ─────────────────────────────────────────┐
│  input polling                                        │
│  PhysicsResetInterpolatedTransformPosition            │
│    → u interpolovaných RB vrátí transform zpět        │
│      na skutečnou fyzikální pózu                      │
└───────────────────────────────────────────────────────┘
┌─ FixedUpdate blok — 0..N× za snímek ──────────────────┐
│  (while akumulovaný čas >= fixedDeltaTime)            │
│   0. Time.fixedTime += fixedDeltaTime                 │
│      uvnitř bloku: Time.time == fixedTime,            │
│      Time.deltaTime == fixedDeltaTime                 │
│   1. FixedUpdate() na všech skriptech                 │
│   2. PhysicsFixedUpdate = Physics.Simulate()          │
│        - solver, integrace, kolize                    │
│        - ZDE se přepíší Transformy rigidbodies        │
│   3. OnTriggerXxx / OnCollisionXxx  (uvnitř simulace) │
│   4. yield WaitForFixedUpdate                         │
└───────────────────────────────────────────────────────┘
┌─ PreUpdate ───────────────────────────────────────────┐
│  PhysicsUpdate → aplikace INTERPOLACE                 │
│    transform dostane vyhlazenou pozici pro tenhle     │
│    snímek (mezi poslední dva fyzikální kroky)         │
└───────────────────────────────────────────────────────┘
┌─ Update ──────────────────────────────────────────────┐
│  Update() na skriptech   ← tady běží Game.Update()    │
│  coroutines: yield null, WaitForSeconds               │
└───────────────────────────────────────────────────────┘
┌─ PreLateUpdate ───────────────────────────────────────┐
│  Animator / Playable graph vyhodnocení                │
│    OnAnimatorMove, OnAnimatorIK                       │
│  LateUpdate()                                         │
│  Constraints                                          │
└───────────────────────────────────────────────────────┘
┌─ PostLateUpdate ──────────────────────────────────────┐
│  UpdateRectTransform, canvasy, particles, skinning    │
│  ► VYKRESLOVÁNÍ (culling + render)                    │
│      built-in: OnPreCull → OnPreRender → draw →       │
│                OnPostRender → OnRenderImage           │
│      HDRP/SRP: RenderPipelineManager.                 │
│                beginCameraRendering / endCameraRend.  │
│  yield WaitForEndOfFrame                              │
│  OnGUI / Gizmos (editor)                              │
└───────────────────────────────────────────────────────┘
```
