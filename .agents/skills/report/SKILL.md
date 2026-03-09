---
name: report
description: "Generate Korean language bidding analysis reports. This skill should be used when converting analysis JSON results into human-readable Korean reports focused on win probability."
allowed-tools: Bash, Read, Write
---

# Report

## Purpose

Transform analysis results into Korean reports emphasizing first-place probability, competition density, and optimal bid rates.

## When to Use

Use this skill when:
- Analysis complete and results need interpretation
- Korean language report needed
- Visualization of competition density required

## How to Use

### Input Requirements

This skill reads from `bidding_context.json` which contains:
- `공고번호`: Announcement number
- `기초금액`: Base amount
- `발주처투찰률`: Agency bid rate
- `발주처`: Procuring agency

### Execution Process

1. **Read Context**: Extract information from `data분석/bidding_context.json`
2. **Locate Data**: Find preprocessed data file
3. **Execute Script**: Run the analysis script from scripts directory
4. **Generate Output**: Create JSON results and visualizations

### Scripts

- `scripts/generate_report.py`

### Integration

This skill integrates with the bidding analysis ecosystem and may be triggered as part of the automated pipeline or run independently for specific analysis needs.
