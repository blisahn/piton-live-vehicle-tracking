/**
 * The small images the map layers use, drawn on a canvas at start-up instead of shipped as files.
 * Everything is drawn at twice the size and registered with pixelRatio 2 so it stays sharp on dense screens.
 */
const PIXEL_RATIO = 2;

type Point = [x: number, y: number];

function drawPolygon(size: number, points: Point[], fill: string, stroke: string): ImageData {
  const canvas = document.createElement("canvas");
  canvas.width = canvas.height = size * PIXEL_RATIO;
  const context = canvas.getContext("2d");
  if (!context) throw new Error("2D canvas is not available");

  context.scale(PIXEL_RATIO, PIXEL_RATIO);
  context.beginPath();
  points.forEach(([x, y], index) => (index === 0 ? context.moveTo(x, y) : context.lineTo(x, y)));
  context.closePath();
  context.fillStyle = fill;
  context.fill();
  context.lineWidth = 1.25;
  context.lineJoin = "round";
  context.strokeStyle = stroke;
  context.stroke();
  return context.getImageData(0, 0, canvas.width, canvas.height);
}

/**
 * An arrowhead pointing up (north), sitting `gap` pixels away from the centre of the image.
 * The layer rotates the image around its centre by the vehicle's bearing, so the arrowhead
 * ends up just outside the vehicle's circle, on the side it is heading to.
 */
export function headingArrow(circleRadius: number, length: number, color: string): ImageData {
  const size = (circleRadius + length + 2) * 2;
  const centre = size / 2;
  const base = centre - circleRadius;
  const halfWidth = length * 0.62;
  return drawPolygon(
    size,
    [
      [centre, base - length],
      [centre + halfWidth, base],
      [centre - halfWidth, base],
    ],
    color,
    "#ffffff",
  );
}

/** An arrowhead pointing right: placed along a line, "right" is the direction the line was drawn in. */
export function travelArrow(color: string): ImageData {
  return drawPolygon(
    14,
    [
      [11, 7],
      [4, 3],
      [4, 11],
    ],
    "#ffffff",
    color,
  );
}

export const ICON_PIXEL_RATIO = PIXEL_RATIO;

/** Size of a mode pictogram in CSS pixels; it sits on top of the vehicle's coloured circle. */
export const PICTOGRAM_SIZE = 14;

type PictogramMode = "bus" | "coach" | "rail" | "tram" | "metro" | "water";

/**
 * A white pictogram on a transparent background, drawn on a 14 × 14 grid. The colour comes from
 * the circle underneath, so one image per mode is enough and the selected (red) vehicle reuses it.
 */
export function modePictogram(mode: PictogramMode): ImageData {
  const canvas = document.createElement("canvas");
  canvas.width = canvas.height = PICTOGRAM_SIZE * PIXEL_RATIO;
  const c = canvas.getContext("2d");
  if (!c) throw new Error("2D canvas is not available");
  c.scale(PIXEL_RATIO, PIXEL_RATIO);
  c.fillStyle = "#ffffff";
  c.strokeStyle = "#ffffff";
  c.lineCap = "round";

  // Windows are cut out of the white body so the circle's colour shows through.
  const cutOut = (x: number, y: number, w: number, h: number) => {
    c.globalCompositeOperation = "destination-out";
    c.fillRect(x, y, w, h);
    c.globalCompositeOperation = "source-over";
  };

  switch (mode) {
    case "bus":
    case "coach":
      c.beginPath();
      c.roundRect(3, 2.5, 8, 8.5, 1.5);
      c.fill();
      cutOut(4.2, 3.8, 5.6, 3);
      c.beginPath();
      c.arc(5, 11.6, 1.1, 0, Math.PI * 2);
      c.arc(9, 11.6, 1.1, 0, Math.PI * 2);
      c.fill();
      break;
    case "rail":
    case "tram":
      if (mode === "tram") {
        c.lineWidth = 1;
        c.beginPath();
        c.moveTo(5, 1);
        c.lineTo(9, 1);
        c.moveTo(7, 1);
        c.lineTo(7, 2.5);
        c.stroke();
      }
      c.beginPath();
      c.roundRect(3.5, 2.5, 7, 8, [3, 3, 1, 1]);
      c.fill();
      cutOut(4.8, 4.2, 4.4, 2.6);
      c.lineWidth = 1.2;
      c.beginPath();
      c.moveTo(5, 11);
      c.lineTo(3.8, 13);
      c.moveTo(9, 11);
      c.lineTo(10.2, 13);
      c.stroke();
      break;
    case "metro":
      c.font = "bold 11px sans-serif";
      c.textAlign = "center";
      c.textBaseline = "middle";
      c.fillText("T", 7, 7.5);
      break;
    case "water":
      c.beginPath();
      c.moveTo(1.5, 8);
      c.lineTo(12.5, 8);
      c.lineTo(10.5, 11.5);
      c.lineTo(3.5, 11.5);
      c.closePath();
      c.fill();
      c.fillRect(4.5, 4.5, 5, 3.5);
      c.fillRect(6.5, 2, 1.2, 2.5);
      break;
  }
  return c.getImageData(0, 0, canvas.width, canvas.height);
}
