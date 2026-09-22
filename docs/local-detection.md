# Local (built-in) Object Detection

AI Tool can run YOLOv8/v11 object detection in-process via ONNX Runtime (`Local_ONNX` AI server type) -
no external AI server, container, or manual model download required. On a fresh install, if no AI servers
are configured at all, a "Local YOLO (built-in)" server is added automatically.

## The default model

* The default model file is `yolov8n.onnx` (the smallest, fastest YOLOv8 model, trained on the 80-class
  COCO dataset).
* It is **downloaded automatically** the first time detection runs, from the URL in the
  `OnnxDefaultModelUrl` setting, to the path in `OnnxDefaultModelPath`
  (`<settings folder>\models\yolov8n.onnx` by default). It is not bundled with AI Tool.
* License: the model is [Ultralytics YOLOv8](https://github.com/ultralytics/ultralytics), licensed
  **AGPL-3.0**. AI Tool itself is licensed under the GNU GPL v2 (see `LICENSE`); because the model is
  downloaded at runtime rather than bundled or linked into the application, it stays a separate,
  independently-licensed artifact rather than becoming part of AI Tool's own distribution. If you
  redistribute AI Tool together with this model file, or use it in a way that triggers AGPL-3.0's terms,
  make sure you comply with that license (or export/host your own model under different terms - see
  below).

## Using a different or custom model

Settings > AI SERVERS > Edit server, for a `Local_ONNX` server:

* **Model Path** - the path to a `.onnx` file exported from a YOLOv8/v11 model. Export one with
  [Ultralytics](https://docs.ultralytics.com/modes/export/):

  ```
  pip install ultralytics
  yolo export model=yolo11n.pt format=onnx
  ```

  This also works with your own custom-trained YOLOv8/v11 model.
* **Custom class names** - if the model isn't the stock 80-class COCO model, put a sidecar file next to
  it named `<model>.names` (or `<model>.txt`), one class name per line, in the same order the model was
  trained with. Without a sidecar file, the stock COCO class list is assumed.
* **Use GPU (DirectML)** - tries DirectML (GPU) first, falling back to CPU if it isn't available.

## Self-hosting the default model

To have AI Tool auto-download a different file as the *default* model (e.g. your own mirror, or a
different model entirely), change `OnnxDefaultModelUrl` in the settings JSON to point at it. It's
downloaded once, on first use, to `OnnxDefaultModelPath`; delete the file at that path (or change
`OnnxDefaultModelPath`) to force a re-download.
