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
