/**
 * Two consecutive reports further apart than this are drawn as a gap (faded and dotted),
 * because the line between them was never measured.
 *
 * Chosen from the live feed (17–18 Nov, 1.76 million intervals within the same trip): the median
 * interval is 10 s, the 99th percentile 60 s, and the sparsest operators that report regularly
 * do so every ~60 s. Twice that marks only 0.36 % of intervals and still catches real silences
 * (a 270 s tunnel near Bergen, a 138 s hole on a Kolumbus bus). Distance is not used: trains
 * cover 1.5–2 km between normal reports, so a distance rule would flag them all the time.
 */
export const TRACK_GAP_SECONDS = 120;
