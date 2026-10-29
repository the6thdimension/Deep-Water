# Trained brains (RH Aviator A17, A18; TRAINING-CONCEPT M19, M33, M34)

- `Latest/Fighter.onnx` is the newest brain. The auto-trainer copies it here after every training session, with
  `Fighter.json` beside it: the run, its steps, the senses it reads, who flew each side, the reward and the settings.
  It is git-ignored, because it changes every session. A learning jet flies it whenever no trainer runs.
- `Kept/<name>.onnx` is a brain worth keeping, with `<name>.json`, its record. Fighter Brains window, "Keep this
  brain…". Commit it to keep it (M19). Any jet can fly a kept brain: set its brain to Saved and pick it.
- A brain trained on another version of the senses does not fit. The jet flies the scripted stand-in, and the
  Console and the window say so.

The trainer's own results (checkpoints, TensorBoard logs) stay in `Training/results/` (git-ignored). The auto-trainer
keeps one rolling run per config there: `auto-selfplay` and `auto-vs-rest`. A run that is set aside is renamed
`<run>.pre-<reason>-backup-<date>`, never deleted.
