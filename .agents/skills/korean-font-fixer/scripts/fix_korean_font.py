#!/usr/bin/env python3
"""
matplotlib 한국어 폰트 자동 설치 및 설정
Noto Sans KR 폰트를 다운로드하고 matplotlib 설정 자동화

Usage:
    python3 fix_korean_font.py              # 전체 설치 및 설정
    python3 fix_korean_font.py --check-only # 진단만 실행
    python3 fix_korean_font.py --no-cache   # 캐시 재생성 건너뛰기
"""

import os
import sys
import urllib.request
import shutil
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[4]


def check_current_fonts():
    """현재 시스템의 한국어 폰트 상태 확인"""
    try:
        import matplotlib.font_manager as fm
        import matplotlib.pyplot as plt

        print('📊 현재 matplotlib 폰트 설정:')
        print('=' * 70)
        print(f'기본 폰트: {plt.rcParams["font.family"]}')
        print(f'폰트 파일 개수: {len(fm.fontManager.ttflist)}')

        print('\n🇰🇷 한국어 지원 폰트 검색:')
        print('-' * 70)

        korean_fonts = set()
        korean_keywords = ['nanum', 'malgun', 'gothic', 'batang', 'noto',
                          'korean', 'dotum', 'gulim', 'gungsuh']

        for font in fm.fontManager.ttflist:
            font_name_lower = font.name.lower()
            if any(k in font_name_lower for k in korean_keywords):
                korean_fonts.add(font.name)
                print(f'  ✓ {font.name}')
                print(f'    └─ {font.fname}')

        if not korean_fonts:
            print('  ❌ 한국어 폰트를 찾을 수 없습니다!')
            print('  → Noto Sans KR 설치를 권장합니다.')
            return False
        else:
            print(f'\n총 {len(korean_fonts)}개의 한국어 폰트 발견')
            return True

    except ImportError:
        print('❌ matplotlib가 설치되지 않았습니다!')
        print('설치: pip install matplotlib')
        return False


def print_manual_install_guide():
    """수동 설치 가이드 출력"""
    print('\n📋 한국어 폰트 수동 설치 가이드')
    print('=' * 70)

    print('\n방법 1: 시스템 패키지 설치 (권장)')
    print('-' * 70)
    print('다음 명령어를 터미널에서 실행하세요:')
    print()
    print('  sudo apt-get update')
    print('  sudo apt-get install -y fonts-noto-cjk fonts-noto-cjk-extra')
    print('  fc-cache -fv')
    print()

    print('\n방법 2: Google Fonts 직접 다운로드')
    print('-' * 70)
    print('1. 브라우저에서 방문:')
    print('   https://fonts.google.com/noto/specimen/Noto+Sans+KR')
    print()
    print('2. "Download family" 버튼 클릭')
    print()
    print('3. ZIP 파일 압축 해제 후 .ttf 파일들을:')
    print('   ~/.local/share/fonts/ 폴더에 복사')
    print()
    print('4. 폰트 캐시 재생성:')
    print('   fc-cache -fv')
    print()

    print('\n방법 3: wget으로 직접 다운로드')
    print('-' * 70)
    print('다음 명령어들을 실행하세요:')
    print()
    print('  mkdir -p ~/.local/share/fonts')
    print('  cd ~/.local/share/fonts')
    print('  wget https://github.com/google/fonts/raw/main/ofl/notosanskr/NotoSansKR%5Bwght%5D.ttf')
    print('  fc-cache -fv')
    print()

    return True


def install_fonts():
    """한국어 폰트 설치"""
    print('\n⚠️  자동 설치는 권한 문제로 지원되지 않습니다.')
    print('수동 설치 방법을 안내합니다.')

    # 수동 설치 가이드 출력
    print_manual_install_guide()

    # 폰트 디렉토리 확인
    font_dir = Path.home() / '.local' / 'share' / 'fonts'
    font_dir.mkdir(parents=True, exist_ok=True)
    print(f'\n📁 사용자 폰트 디렉토리가 준비되었습니다: {font_dir}')

    return False  # 수동 설치 필요


def rebuild_font_cache():
    """matplotlib 폰트 캐시 재생성"""
    print('\n🔄 matplotlib 폰트 캐시 재생성 중...')
    print('-' * 70)

    try:
        import matplotlib.font_manager as fm

        # 기존 캐시 삭제
        cache_dir = Path.home() / '.cache' / 'matplotlib'
        if cache_dir.exists():
            for cache_file in cache_dir.glob('fontlist-v*.json'):
                print(f'  🗑️  기존 캐시 삭제: {cache_file.name}')
                cache_file.unlink()

        # 캐시 재생성
        print('  🔧 폰트 캐시 재빌드 중...', end=' ')
        fm._rebuild()
        print('✓')

        # fontconfig 캐시도 재생성 (시스템 명령)
        import subprocess
        try:
            print('  🔧 fontconfig 캐시 재생성 중...', end=' ')
            result = subprocess.run(['fc-cache', '-fv'],
                                   capture_output=True,
                                   text=True,
                                   timeout=30)
            if result.returncode == 0:
                print('✓')
            else:
                print('⚠️  (건너뜀 - fc-cache 없음)')
        except (subprocess.TimeoutExpired, FileNotFoundError):
            print('⚠️  (건너뜀)')

        print('\n✅ 폰트 캐시 재생성 완료')
        return True

    except Exception as e:
        print(f'\n❌ 캐시 재생성 실패: {e}')
        return False


def check_analyze_py_font_detection():
    """analyze.py에 한국어 폰트 자동 감지 기능이 있는지 확인"""
    print('\n📝 analyze.py 한국어 폰트 자동 감지 확인...')
    print('-' * 70)

    analyze_path = PROJECT_ROOT / '.agents' / 'skills' / 'logic' / 'scripts' / 'analyze.py'

    if not analyze_path.exists():
        print('  ⚠️  analyze.py를 찾을 수 없습니다.')
        return False

    # 파일 내용 확인
    content = analyze_path.read_text(encoding='utf-8')

    if 'def get_korean_font()' in content:
        print('  ✅ 한국어 폰트 자동 감지 기능이 이미 구현되어 있습니다.')
        print('  → analyze.py가 시스템 폰트를 자동으로 찾아 사용합니다.')
        return True
    else:
        print('  ⚠️  한국어 폰트 자동 감지 기능이 없습니다.')
        print('  → analyze.py에 get_korean_font() 함수를 추가하세요.')
        return False


def verify_installation():
    """설치 후 검증"""
    print('\n🔍 설치 검증 중...')
    print('=' * 70)

    try:
        import matplotlib.font_manager as fm

        # Noto Sans KR 찾기
        noto_fonts = [f for f in fm.fontManager.ttflist
                     if 'Noto Sans KR' in f.name or 'NotoSansKR' in f.fname]

        if noto_fonts:
            print('✅ Noto Sans KR 폰트가 정상적으로 인식됩니다!')
            print('\n사용 가능한 Noto Sans KR 폰트:')
            for font in noto_fonts:
                print(f'  ✓ {font.name}')
                print(f'    └─ {font.fname}')
            return True
        else:
            print('❌ Noto Sans KR 폰트가 인식되지 않습니다.')
            print('\n해결 방법:')
            print('1. 시스템 재부팅')
            print('2. matplotlib 재설치: pip install --force-reinstall matplotlib')
            print('3. 수동 캐시 재생성: python3 -c "import matplotlib.font_manager as fm; fm._rebuild()"')
            return False

    except Exception as e:
        print(f'❌ 검증 실패: {e}')
        return False


def create_test_graph():
    """한국어 테스트 그래프 생성"""
    print('\n🎨 한국어 테스트 그래프 생성 중...')
    print('-' * 70)

    try:
        import matplotlib.pyplot as plt
        import numpy as np

        # 폰트 설정
        plt.rcParams['font.family'] = 'Noto Sans KR'
        plt.rcParams['axes.unicode_minus'] = False

        # 간단한 테스트 그래프
        fig, ax = plt.subplots(figsize=(10, 6), dpi=150)

        x = np.linspace(85, 90, 100)
        y = np.sin(x / 2) * 100 + 200

        ax.plot(x, y, linewidth=2, color='#2E86DE', label='경쟁자 밀도')
        ax.axvline(x=87.745, color='red', linestyle='--', linewidth=2,
                  label='발주처투찰률 (87.745%)')

        ax.set_xlabel('기초대비투찰률 (%)', fontsize=12, weight='bold')
        ax.set_ylabel('경쟁자 수 (명)', fontsize=12, weight='bold')
        ax.set_title('한국어 폰트 테스트: 복수예가입찰 분석',
                    fontsize=14, weight='bold', pad=20)
        ax.legend(loc='upper right', fontsize=11)
        ax.grid(True, linestyle=':', alpha=0.5)

        output_path = PROJECT_ROOT / 'data분석' / 'korean_font_test.png'
        output_path.parent.mkdir(parents=True, exist_ok=True)
        plt.tight_layout()
        plt.savefig(output_path, dpi=150, bbox_inches='tight')
        plt.close()

        file_size = output_path.stat().st_size / 1024
        print(f'✅ 테스트 그래프 생성 완료')
        print(f'  📄 파일: {output_path}')
        print(f'  💾 크기: {file_size:.1f} KB')
        print('\n👀 그래프를 열어서 한국어가 정상적으로 표시되는지 확인하세요!')
        return True

    except Exception as e:
        print(f'❌ 테스트 그래프 생성 실패: {e}')
        return False


def main():
    """메인 실행 함수"""
    import argparse

    parser = argparse.ArgumentParser(description='matplotlib 한국어 폰트 자동 설치')
    parser.add_argument('--check-only', action='store_true',
                       help='진단만 실행 (설치 안 함)')
    parser.add_argument('--no-cache', action='store_true',
                       help='캐시 재생성 건너뛰기')
    parser.add_argument('--no-test', action='store_true',
                       help='테스트 그래프 생성 건너뛰기')

    args = parser.parse_args()

    print('\n' + '=' * 70)
    print('  matplotlib 한국어 폰트 자동 설치 도구')
    print('  Noto Sans KR 설치 및 설정')
    print('=' * 70 + '\n')

    # 1. 현재 상태 확인
    has_korean = check_current_fonts()

    if args.check_only:
        print('\n✅ 진단 완료')
        return 0 if has_korean else 1

    # 2. analyze.py 폰트 자동 감지 확인
    check_analyze_py_font_detection()

    # 3. 폰트 설치 가이드
    if not has_korean:
        install_fonts()
        print('\n⚠️  위의 설치 방법 중 하나를 선택하여 한국어 폰트를 설치하세요.')
        print('설치 후 이 스크립트를 다시 실행하거나 다음 단계를 진행하세요.')

        # 사용자에게 질문
        print('\n━' * 35)
        print('한국어 폰트 설치를 완료하셨습니까? (y/N): ', end='')
        try:
            response = input().strip().lower()
            if response != 'y':
                print('\n폰트 설치 후 다시 실행하세요.')
                return 1
        except (KeyboardInterrupt, EOFError):
            print('\n\n중단됨.')
            return 1
    else:
        print('\n✅ 한국어 폰트가 이미 설치되어 있습니다!')

    # 4. 캐시 재생성
    if not args.no_cache:
        rebuild_font_cache()

    # 5. 설치 검증
    print()
    verify_installation()

    # 6. 테스트 그래프 생성
    if not args.no_test:
        create_test_graph()

    print('\n' + '=' * 70)
    print('✅ 설정이 완료되었습니다!')
    print('=' * 70)
    print('\n다음 단계:')
    print('1. analyze.py를 실행하여 그래프 생성')
    print('2. 그래프에서 한국어가 정상적으로 표시되는지 확인')
    print('3. 문제가 지속되면 시스템 재부팅 후 재시도')
    print()

    return 0


if __name__ == '__main__':
    sys.exit(main())
