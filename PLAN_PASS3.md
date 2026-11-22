# PASS 3: Environment, humanoid ragdoll NPCs, reactive faces

Design decisions (user may overrule):
- D1. Sky: procedural skybox material made in code (`Skybox/Procedural` or gradient via 6-sided is NOT used). Cartoon colours, bright sun, light fog.
- D2. Floor: keep the existing 20x20 Floor collider. Add a tiled checker texture generated in code (Texture2D, no files) plus a large visual-only ground plane outside the walls. No new colliders.
- D3. Materials: follow unity skill (no Shader.Find for runtime materials; use an existing material source in the repo).
- D4. NPC body: build in code from primitives: torso, head, 2 upper arms, 2 forearms, 2 thighs, 2 shins. Joints: CharacterJoint with limits. Keep the single-root-rigidbody "balance" mode working until ragdoll is proven; ragdoll state = joints active, balance force off.
- D5. Ragdoll trigger: strong slap or leg sweep switches to ragdoll for a few seconds, then NPC recovers (existing recover logic).
- D6. Faces: eyes (two spheres + pupils), brow bars, mouth (scaled capsule/cube). Expressions: Idle, Hit (cross eyes, wide mouth), Dizzy (spin pupils), Recover. Driven by a small FaceController MonoBehaviour.
- D7. Sounds: user adds clips later. Code must tolerate empty banks (already true).

Phases (each task: 1-3 files, workers do not commit):
1. Environment: GameBootstrap.cs (+ new EnvironmentBuilder.cs in Scripts/Core).
2. NPC recon (read-only): how NpcController builds the body, balance, slap response.
3. NpcBodyBuilder.cs (new): humanoid hierarchy + joints, no behaviour change.
4. FaceController.cs (new): face parts and expressions.
5. Wiring: NpcController hooks (ragdoll switch, face reactions).
6. Review gate (read-only), fix pass, user tuning.
