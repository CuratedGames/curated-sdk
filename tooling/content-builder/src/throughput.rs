//! Adaptive chunk size and upload concurrency from measured bandwidth.

/// Keep in sync with `apps/api` `builds.rs` (fallback when limits endpoint is unavailable).
pub const MIN_CHUNK_SIZE: i64 = 65_536;
pub const DEFAULT_CHUNK_SIZE: i64 = 8_388_608;
pub const MAX_CHUNK_SIZE: i64 = 33_554_432;
pub const DEFAULT_CONCURRENCY: usize = 4;
pub const MAX_CONCURRENCY: usize = 8;
/// Probe payload size for bandwidth sampling (≤ server `max_bandwidth_probe_bytes`).
pub const BANDWIDTH_PROBE_BYTES: usize = 524_288; // 512 KiB
/// Target seconds of transfer per chunk (balances request overhead vs proxy timeouts).
pub const TARGET_CHUNK_SECONDS: f64 = 4.0;

/// Round `n` down to the nearest power of two (minimum 1).
pub fn round_down_pow2(n: i64) -> i64 {
    if n <= 1 {
        return 1;
    }
    let mut x = n as u64;
    x |= x >> 1;
    x |= x >> 2;
    x |= x >> 4;
    x |= x >> 8;
    x |= x >> 16;
    x |= x >> 32;
    (x - (x >> 1)) as i64
}

/// Pick a chunk size from measured bytes/sec, clamped to server limits.
///
/// When `bytes_per_sec` is `None`, returns `default_chunk` (already within limits).
pub fn choose_chunk_size(
    bytes_per_sec: Option<f64>,
    min_chunk: i64,
    max_chunk: i64,
    default_chunk: i64,
) -> i64 {
    let min_chunk = min_chunk.max(1);
    let max_chunk = max_chunk.max(min_chunk);
    let default_chunk = default_chunk.clamp(min_chunk, max_chunk);
    let Some(bps) = bytes_per_sec.filter(|v| v.is_finite() && *v > 0.0) else {
        return default_chunk;
    };
    let raw = (bps * TARGET_CHUNK_SECONDS).round() as i64;
    let rounded = round_down_pow2(raw).clamp(min_chunk, max_chunk);
    // If rounding dropped below min, use min (not another pow2 below the floor).
    if rounded < min_chunk {
        min_chunk
    } else {
        rounded
    }
}

/// Parallel PUT count from bandwidth and chunk size, capped by server advice.
pub fn choose_concurrency(
    bytes_per_sec: Option<f64>,
    chunk_size: i64,
    max_concurrent: usize,
) -> usize {
    let max_concurrent = max_concurrent.clamp(1, MAX_CONCURRENCY);
    let chunk_size = chunk_size.max(1) as f64;
    let Some(bps) = bytes_per_sec.filter(|v| v.is_finite() && *v > 0.0) else {
        return DEFAULT_CONCURRENCY.min(max_concurrent);
    };
    // Keep roughly ~TARGET_CHUNK_SECONDS of data in flight across workers.
    let in_flight = bps * TARGET_CHUNK_SECONDS;
    let n = (in_flight / chunk_size).ceil() as usize;
    n.clamp(1, max_concurrent)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn round_down_pow2_examples() {
        assert_eq!(round_down_pow2(1), 1);
        assert_eq!(round_down_pow2(7), 4);
        assert_eq!(round_down_pow2(8), 8);
        assert_eq!(round_down_pow2(9), 8);
        assert_eq!(round_down_pow2(8_388_608), 8_388_608);
        assert_eq!(round_down_pow2(10_000_000), 8_388_608);
    }

    #[test]
    fn choose_chunk_size_uses_default_without_measurement() {
        assert_eq!(
            choose_chunk_size(None, MIN_CHUNK_SIZE, MAX_CHUNK_SIZE, DEFAULT_CHUNK_SIZE),
            DEFAULT_CHUNK_SIZE
        );
        assert_eq!(
            choose_chunk_size(Some(0.0), MIN_CHUNK_SIZE, MAX_CHUNK_SIZE, DEFAULT_CHUNK_SIZE),
            DEFAULT_CHUNK_SIZE
        );
    }

    #[test]
    fn choose_chunk_size_scales_with_bandwidth() {
        // ~1 MiB/s → ~4 MiB target → 4 MiB pow2
        let slow = choose_chunk_size(
            Some(1_048_576.0),
            MIN_CHUNK_SIZE,
            MAX_CHUNK_SIZE,
            DEFAULT_CHUNK_SIZE,
        );
        assert_eq!(slow, 4_194_304);

        // ~20 MiB/s → 80 MiB raw → clamp to 32 MiB max
        let fast = choose_chunk_size(
            Some(20.0 * 1_048_576.0),
            MIN_CHUNK_SIZE,
            MAX_CHUNK_SIZE,
            DEFAULT_CHUNK_SIZE,
        );
        assert_eq!(fast, MAX_CHUNK_SIZE);

        // Tiny link → floor at min
        let tiny = choose_chunk_size(
            Some(1_000.0),
            MIN_CHUNK_SIZE,
            MAX_CHUNK_SIZE,
            DEFAULT_CHUNK_SIZE,
        );
        assert_eq!(tiny, MIN_CHUNK_SIZE);
    }

    #[test]
    fn choose_concurrency_scales_and_caps() {
        assert_eq!(
            choose_concurrency(None, DEFAULT_CHUNK_SIZE, MAX_CONCURRENCY),
            DEFAULT_CONCURRENCY
        );
        // Fast pipe + small chunks → hit cap
        assert_eq!(
            choose_concurrency(Some(50.0 * 1_048_576.0), MIN_CHUNK_SIZE, 8),
            8
        );
        // Slow pipe + large chunks → single worker
        assert_eq!(
            choose_concurrency(Some(100_000.0), MAX_CHUNK_SIZE, 8),
            1
        );
    }
}
