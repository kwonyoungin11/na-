---
name: korean-font-fixer
description: "Fix matplotlib Korean font rendering issues. This skill should be used when matplotlib displays Korean characters as broken squares or boxes."
allowed-tools: Bash, Read, Write
---

# Korean Font Fixer

## Purpose

Automatically download Noto Sans KR font, regenerate matplotlib font cache, and apply Korean font settings.

## When to Use

Use this skill when:
- Korean characters display as squares in matplotlib
- Font errors or warnings appear
- Malgun Gothic font not available

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

- `scripts/fix_korean_font.py`

### Integration

This skill integrates with the bidding analysis ecosystem and may be triggered as part of the automated pipeline or run independently for specific analysis needs.
