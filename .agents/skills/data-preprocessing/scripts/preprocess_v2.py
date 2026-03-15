#!/usr/bin/env python3
"""
나라장터 복수예가입찰 데이터 전처리 스크립트 V2
수정.md 요구사항 완전 구현 버전
"""

import os
import re
import pandas as pd
import numpy as np
from datetime import datetime
from pathlib import Path
import sys
import warnings
warnings.filterwarnings('ignore')

PROJECT_ROOT = Path(__file__).resolve().parents[4]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

from nara_workspace_paths import master_dir, source_data_dir

DEFAULT_SOURCE_DIR = source_data_dir()
DEFAULT_OUTPUT_DIR = master_dir()

class BiddingPreprocessor:
    """
    나라장터 입찰 데이터 전처리기
    수정.md의 모든 요구사항 구현
    증분 업데이트 지원 (신규 데이터만 처리)
    """

    def __init__(self, source_dir=None, output_dir=None, force=False, region_scope='core3'):
        self.source_dir = Path(source_dir) if source_dir else DEFAULT_SOURCE_DIR
        self.output_dir = Path(output_dir) if output_dir else DEFAULT_OUTPUT_DIR
        self.output_dir.mkdir(exist_ok=True)
        self.force = force  # 전체 재처리 모드
        self.region_scope = region_scope  # core3 | nationwide(legacy) | all

        # 로그 파일 - append 모드 (기존 로그 보존)
        self.log_file = open(self.output_dir / "preprocessing_log.txt", "a", encoding='utf-8')

        # 기존 처리된 데이터 로드
        self.existing_df = None
        self.processed_announcements = set()
        if not force:
            self.load_existing_data()

    def load_existing_data(self):
        """기존 처리된 데이터 로드 (증분 업데이트용)"""
        master_file = self.output_dir / "전체_통합_데이터.xlsx"
        try:
            if master_file.exists():
                self.existing_df = pd.read_excel(master_file, engine='openpyxl')
                self.processed_announcements = set(self.existing_df['공고번호'].astype(str).unique())
                self.log_message(f"기존 데이터 로드: {len(self.processed_announcements)}개 공고, {len(self.existing_df)}건")
            else:
                self.log_message("기존 데이터 없음 - 전체 처리 모드")
        except Exception as e:
            self.log_message(f"기존 데이터 로드 실패: {str(e)} - 전체 처리 모드로 전환")
            self.existing_df = None
            self.processed_announcements = set()

    def log_message(self, msg):
        """로그 메시지 기록"""
        timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        log_entry = f"[{timestamp}] {msg}"
        print(log_entry)
        self.log_file.write(log_entry + "\n")
        self.log_file.flush()

    def normalize_company_name(self, name):
        """
        2️⃣ 업체명 정규화 규칙
        """
        if pd.isna(name):
            return ""

        name = str(name).strip()

        # 법인 형태 제거 (순서 중요)
        patterns = [
            r'\(재\)', r'재단법인', r'재단',
            r'\(주\)', r'주식회사',
            r'\(사\)', r'사단법인',
            r'\(유\)', r'유한회사',
            r'\(합\)', r'합명회사',
            r'\(합자\)', r'합자회사'
        ]

        for pattern in patterns:
            name = re.sub(pattern, '', name)

        # 문화재 관련 통일
        name = re.sub(r'문화유산연구원', '문화재연구원', name)
        name = re.sub(r'문화재보존센터', '문화재연구원', name)
        name = re.sub(r'고고학연구소', '문화재연구원', name)

        # 특정 업체명 보정
        if '문화유산마을' in name and '문화재연구원' not in name:
            name = '문화유산마을 문화재연구원'

        # 특수문자 정규화
        name = name.replace('&', 'and')

        # 다중 공백을 단일 공백으로
        name = re.sub(r'\s+', ' ', name)

        return name.strip()

    def normalize_region_text(self, value):
        """지역제한 텍스트 정규화"""
        if pd.isna(value):
            return ""
        region = str(value).strip()
        region = region.replace('\n', ' ').replace('\r', ' ')
        region = re.sub(r'\s+', ' ', region).strip()
        return region

    def is_allowed_region(self, region_value):
        """지역제한 허용 여부 판단"""
        if self.region_scope == 'all':
            return True

        region = self.normalize_region_text(region_value)
        if not region:
            return False

        # 기본 정책: 전국/대구/경북 허용
        return (
            ('전국' in region)
            or ('대구' in region)
            or ('경북' in region)
            or ('경상북' in region)
        )

    def parse_money_amount(self, value):
        """
        3️⃣ 금액 관련 필드 파싱 규칙
        """
        if pd.isna(value):
            return 0

        value = str(value)
        # 쉼표, 원, 공백 제거
        value = re.sub(r'[,원\s]', '', value)

        try:
            return int(float(value))
        except:
            return 0

    def parse_percentage(self, value, field_name=""):
        """
        4️⃣ 비율/퍼센트 관련 필드 처리
        """
        if pd.isna(value):
            return None

        value = str(value).replace('%', '').strip()

        # 괄호 안 값 처리
        if '(' in str(value):
            match = re.search(r'\(([0-9.]+)\)', str(value))
            if match:
                value = match.group(1)

        try:
            value = float(value)

            # 기초대비사정률 특별 처리
            if "사정률" in field_name or "기초대비사정률" in field_name:
                if -2 <= value <= 2:
                    return 100 + value
                elif 98 <= value <= 102:
                    return value
                else:
                    self.log_message(f"이상치 발견 - {field_name}: {value}")
                    return None

            return value
        except:
            return None

    def parse_datetime(self, value):
        """
        5️⃣ 날짜/시간 필드 처리
        """
        if pd.isna(value):
            return None

        # 여러 형식 시도
        formats = [
            "%Y-%m-%d %H:%M:%S",
            "%Y-%m-%d %H:%M",
            "%Y/%m/%d %H:%M:%S",
            "%Y.%m.%d %H:%M:%S",
            "%Y-%m-%d",
        ]

        for fmt in formats:
            try:
                return pd.to_datetime(value, format=fmt)
            except:
                continue

        # 모든 형식 실패시 pandas 자동 파싱
        try:
            return pd.to_datetime(value)
        except:
            return None

    def check_file_pair(self, file_path):
        """
        1️⃣ 파일 필터링 규칙 - 공고번호 쌍 체크
        """
        file_name = file_path.stem

        # _참여업체목록 파일인 경우
        if "_참여업체목록" in file_name:
            main_name = file_name.replace("_참여업체목록", "")
            main_file = file_path.parent / f"{main_name}.xlsx"
            return main_file.exists()

        # 메인 파일인 경우
        participant_file = file_path.parent / f"{file_name}_참여업체목록.xlsx"
        return participant_file.exists()

    def extract_bid_info(self, df):
        """
        9️⃣ 공고정보 필수 필드 추출 (키-값 형식)
        """
        info = {}

        # 처음 20행 정도에서 키-값 쌍 추출
        for i in range(min(20, len(df))):
            row = df.iloc[i]
            if pd.notna(row.iloc[0]) and pd.notna(row.iloc[1]):
                key = str(row.iloc[0]).strip()
                value = str(row.iloc[1]).strip()

                if '공고번호' in key:
                    info['공고번호'] = value
                elif '공사명' in key or '용역명' in key:
                    info['공사명'] = value
                elif '발주처' in key:
                    # 담당자 정보 제거
                    info['발주처'] = value.split('(')[0].strip()
                elif '개찰일' in key:
                    info['개찰일시'] = self.parse_datetime(value)
                elif '기초금액' in key:
                    info['기초금액'] = self.parse_money_amount(value)
                elif '예정가격' in key:
                    info['예정가격'] = self.parse_money_amount(value)
                elif '낙찰하한가' in key:
                    info['낙찰하한가'] = self.parse_money_amount(value)
                elif '사정률' in key:
                    info['사정률'] = self.parse_percentage(value, '사정률')
                elif '낙찰하한율' in key or '투찰률' in key:
                    info['낙찰하한율'] = self.parse_percentage(value)
                elif '지역제한' in key:
                    info['지역제한'] = value

        # 사정률 계산 (없는 경우)
        if info.get('기초금액') and info.get('예정가격') and not info.get('사정률'):
            info['사정률'] = ((info['예정가격'] - info['기초금액']) / info['기초금액']) * 100

        # 낙찰하한가 계산 (없는 경우)
        if not info.get('낙찰하한가') and info.get('예정가격') and info.get('낙찰하한율'):
            info['낙찰하한가'] = int(info['예정가격'] * (info['낙찰하한율'] / 100))

        return info

    def find_data_start_row(self, df):
        """
        실제 데이터가 시작하는 행 찾기
        """
        for i in range(len(df)):
            row = df.iloc[i]
            # '순위' 텍스트가 첫 번째 열에 있는 헤더 행 찾기
            if pd.notna(row.iloc[0]) and '순위' == str(row.iloc[0]).strip():
                return i + 1  # 헤더 다음 행부터 데이터
        return 0

    def merge_bidding_files(self, main_file, participant_file):
        """
        메인 파일과 참여업체목록 파일 통합
        18개 필수 컬럼 완전성 체크 (개찰일시 포함)
        """
        try:
            # 파일 읽기 (헤더 없이)
            main_df = pd.read_excel(main_file, header=None, engine='openpyxl')
            participant_df = pd.read_excel(participant_file, header=None, engine='openpyxl')

            # 공고 정보 추출
            bid_info = self.extract_bid_info(main_df)
            participant_bid_info = self.extract_bid_info(participant_df)
            bid_info.update({k: v for k, v in participant_bid_info.items() if v})

            # 지역제한 필터(기본: 전국만)
            region_text = self.normalize_region_text(bid_info.get('지역제한', ''))
            if not self.is_allowed_region(region_text):
                self.log_message(f"지역제한 제외({self.region_scope}): {main_file.name} / 지역제한='{region_text}'")
                return None

            # 실제 데이터 시작 위치 찾기
            data_start = self.find_data_start_row(participant_df)

            if data_start == 0:
                self.log_message(f"데이터 시작 위치를 찾을 수 없음: {main_file.name}")
                return None

            # 헤더와 데이터 분리
            header_row = participant_df.iloc[data_start - 1]
            data_df = participant_df.iloc[data_start:].reset_index(drop=True)

            # 컬럼명 설정
            columns = []
            for val in header_row:
                if pd.notna(val):
                    columns.append(str(val).strip())
                else:
                    columns.append(f"col_{len(columns)}")

            data_df.columns = columns[:len(data_df.columns)]

            # 빈 행 제거
            data_df = data_df.dropna(how='all')

            # 참여업체 수 체크
            if len(data_df) < 5:
                self.log_message(f"참여업체 5개 미만: {main_file.name}")
                return None

            # 업체명은 필터링하지 않고 전처리 단계에서 정규화만 수행
            _ = data_df.get('업체', pd.Series()).apply(self.normalize_company_name)

            # 필수 컬럼 생성
            processed_df = self.create_standard_dataframe(data_df, bid_info)

            # 데이터 품질 검증
            if not self.validate_data_quality(processed_df):
                self.log_message(f"데이터 품질 검증 실패: {main_file.name}")

            return processed_df

        except Exception as e:
            self.log_message(f"파일 통합 실패 {main_file.name}: {str(e)}")
            return None

    def create_standard_dataframe(self, df, bid_info):
        """
        8️⃣ 18개 필수 컬럼 데이터프레임 생성 (개찰일시 포함)
        """
        result = pd.DataFrame()

        # 컬럼 매핑 - 원본 데이터를 그대로 사용
        column_map = {
            '순위': ['순위', 'rank'],
            '업체': ['업체', '업체명', '상호', '회사명'],
            '투찰일시': ['투찰일시', '투찰일', '제출일시'],
            '투찰금액': ['투찰금액', '입찰금액', '투찰가'],
            '예가대비투찰률': ['예가대비투찰률(%)', '예가대비투찰률', '예가대비'],
            '기초대비투찰률': ['기초대비투찰률(%)', '기초대비투찰률', '기초대비'],
            '기초대비사정률': ['기초대비사정률(%)', '기초대비사정률']
        }

        # 각 컬럼 찾아서 매핑
        for target, candidates in column_map.items():
            found = False
            for col in df.columns:
                if any(cand in str(col) for cand in candidates):
                    if '업체' in target:
                        result['업체명'] = df[col].apply(self.normalize_company_name)
                    elif '투찰금액' in target:
                        result['투찰금액'] = df[col].apply(self.parse_money_amount)
                    elif '투찰일시' in target:
                        result['투찰일시'] = df[col].apply(self.parse_datetime)
                    elif '예가대비투찰률' in target:
                        result['예가대비투찰률'] = df[col]
                    elif '기초대비투찰률' in target:
                        result['기초대비투찰률'] = df[col]
                    elif '기초대비사정률' in target:
                        result['기초대비사정률'] = df[col].apply(lambda x: self.parse_percentage(x, '기초대비사정률'))
                    else:
                        result[target] = df[col]
                    found = True
                    break

            if not found:
                # 기본값 설정
                result[target] = None

        # 공고 정보 추가 (공고당 1개씩)
        result['공고번호'] = bid_info.get('공고번호', '')
        result['공고명'] = bid_info.get('공사명', '')
        result['발주처'] = bid_info.get('발주처', '')  # 지역 필터링용
        result['지역제한'] = bid_info.get('지역제한', '')  # 지역 필터링용
        result['개찰일시'] = bid_info.get('개찰일시', None)
        result['예정가격'] = bid_info.get('예정가격', 0)
        result['낙찰하한가'] = bid_info.get('낙찰하한가', 0)
        result['기초금액'] = bid_info.get('기초금액', 0)
        result['사정률'] = bid_info.get('사정률', None)
        result['발주처투찰률'] = bid_info.get('낙찰하한율', None)

        # 투찰일시가 비어있으면 개찰일시로 보정
        if '투찰일시' in result.columns and '개찰일시' in result.columns:
            result['투찰일시'] = pd.to_datetime(result['투찰일시'], errors='coerce')
            result['개찰일시'] = pd.to_datetime(result['개찰일시'], errors='coerce')
            result['투찰일시'] = result['투찰일시'].fillna(result['개찰일시'])

        # 낙찰하한가차이 계산 (원본에 없는 경우만)
        if '투찰금액' in result.columns:
            낙찰하한가 = bid_info.get('낙찰하한가', 0)
            if 낙찰하한가 > 0:
                result['낙찰하한가차이'] = result['투찰금액'] - 낙찰하한가

        # 1️⃣1️⃣ 하한가 미달 데이터 처리 (중요!)
        if '낙찰하한가차이' in result.columns:
            # 미달 업체는 순위 -1
            result.loc[result['낙찰하한가차이'] < 0, '순위'] = -1

        # 필수 컬럼 확인 및 생성 (18개 컬럼 - 개찰일시 추가)
        required_columns = [
            '공고번호', '공고명', '발주처', '지역제한', '순위', '업체명', '개찰일시', '투찰일시',
            '투찰금액', '예가대비투찰률', '기초대비투찰률', '기초대비사정률',
            '예정가격', '낙찰하한가', '기초금액', '사정률', '발주처투찰률', '낙찰하한가차이'
        ]

        for col in required_columns:
            if col not in result.columns:
                result[col] = None

        # 컬럼 순서 정리
        result = result[required_columns]

        return result

    def validate_data_quality(self, df):
        """
        🔟 데이터 품질 검증
        """
        # 중복 업체 체크
        if '사업자등록번호' in df.columns:
            duplicates = df['사업자등록번호'].duplicated()
            if duplicates.any():
                self.log_message(f"중복 업체 발견: {duplicates.sum()}개")

        # 투찰금액 유효성
        if '투찰금액' in df.columns and '기초금액' in df.columns:
            base_amount = df['기초금액'].iloc[0] if len(df) > 0 else 0
            if base_amount > 0:
                invalid_bids = df[(df['투찰금액'] < base_amount * 0.5) |
                                 (df['투찰금액'] > base_amount * 1.2)]
                if len(invalid_bids) > 0:
                    self.log_message(f"비정상 투찰금액 발견: {len(invalid_bids)}개")

        # 추첨번호 유효성
        if '추첨번호' in df.columns:
            for idx, draw_nums in df['추첨번호'].items():
                if pd.notna(draw_nums):
                    try:
                        nums = str(draw_nums).split(',')
                        nums = [int(n.strip()) for n in nums if n.strip()]
                        if len(nums) != 4 or any(n < 1 or n > 15 for n in nums):
                            self.log_message(f"비정상 추첨번호: {draw_nums}")
                    except:
                        pass

        return True

    def process_all_files(self):
        """
        전체 파일 처리 메인 함수
        증분 업데이트: 신규 데이터만 처리
        """
        self.log_message("\n" + "="*50)
        self.log_message("나라장터 복수예가입찰 데이터 전처리 V2 시작")
        self.log_message(f"모드: {'전체 재처리' if self.force else '증분 업데이트'}")
        self.log_message(f"지역제한 범위: {self.region_scope}")
        self.log_message(f"원본 디렉토리: {self.source_dir}")
        self.log_message(f"출력 디렉토리: {self.output_dir}")
        if self.processed_announcements:
            self.log_message(f"기존 처리된 공고: {len(self.processed_announcements)}개")
        self.log_message("="*50)

        # Excel 파일 목록 가져오기
        excel_files = list(self.source_dir.glob("*.xlsx")) + list(self.source_dir.glob("*.xls"))
        self.log_message(f"발견된 Excel 파일 수: {len(excel_files)}")

        # 메인 파일과 참여업체목록 파일 분류
        main_files = []
        skipped_count = 0
        for file in excel_files:
            if "_참여업체목록" not in file.name:
                if self.check_file_pair(file):
                    # 증분 업데이트: 이미 처리된 공고번호 건너뛰기
                    bid_number = file.stem
                    if bid_number in self.processed_announcements:
                        skipped_count += 1
                        continue
                    main_files.append(file)
                else:
                    self.log_message(f"쌍이 없는 파일 제외: {file.name}")

        if skipped_count > 0:
            self.log_message(f"기존 처리된 공고 건너뜀: {skipped_count}개")
        self.log_message(f"신규 처리할 공고 수: {len(main_files)}")

        # 신규 처리할 파일이 없는 경우
        if len(main_files) == 0:
            self.log_message("신규 처리할 데이터가 없습니다. 기존 데이터 지역 필터 재적용을 진행합니다.")
            self.log_message("="*50)
            self.create_master_dataset([])
            return

        # 통계 변수
        success_count = 0
        fail_count = 0
        fail_list = []
        all_data = []  # 개별 파일 저장 대신 메모리에 수집

        # 각 공고별 처리
        for idx, main_file in enumerate(main_files, 1):
            bid_number = main_file.stem
            participant_file = main_file.parent / f"{bid_number}_참여업체목록.xlsx"

            self.log_message(f"\n[{idx}/{len(main_files)}] 처리 중: {bid_number}")

            # 파일 통합 및 처리
            result_df = self.merge_bidding_files(main_file, participant_file)

            if result_df is not None:
                # 개별 파일 저장 안 함 - 메모리에만 저장
                result_df['공고번호'] = bid_number
                all_data.append(result_df)
                self.log_message(f"✓ 처리 완료: {bid_number}")
                success_count += 1
            else:
                self.log_message(f"✗ 처리 실패: {bid_number}")
                fail_count += 1
                fail_list.append(bid_number)

        # 최종 통계
        self.log_message("\n" + "="*50)
        self.log_message("전처리 완료 통계 (증분 업데이트)")
        self.log_message(f"신규 처리 성공: {success_count}개")
        self.log_message(f"신규 처리 실패: {fail_count}개")
        if self.processed_announcements:
            self.log_message(f"기존 데이터: {len(self.processed_announcements)}개 공고")
            self.log_message(f"총 누적 공고: {len(self.processed_announcements) + success_count}개")
        if fail_list:
            self.log_message(f"실패 목록: {', '.join(fail_list[:10])}{'...' if len(fail_list) > 10 else ''}")
        self.log_message("="*50)

        # 전체 통합 데이터 생성
        self.create_master_dataset(all_data)

    def create_master_dataset(self, all_data):
        """
        1️⃣3️⃣ 전체 통합 데이터셋 생성
        개별 파일 저장 없이 메모리의 데이터프레임 리스트로부터 직접 생성
        증분 업데이트: 기존 데이터와 신규 데이터 병합
        """
        self.log_message("\n전체 통합 데이터셋 생성 시작...")

        if not all_data and self.existing_df is None:
            self.log_message("통합할 데이터가 없습니다.")
            return

        # 신규 데이터 병합
        if all_data:
            new_df = pd.concat(all_data, ignore_index=True)
            self.log_message(f"신규 데이터: {len(new_df)}건")

            # 기존 데이터와 병합 (증분 업데이트)
            if self.existing_df is not None and not self.force:
                master_df = pd.concat([self.existing_df, new_df], ignore_index=True)
                self.log_message(f"기존 데이터 {len(self.existing_df)}건 + 신규 {len(new_df)}건 병합")

                # 중복 제거 (공고번호 + 업체명 기준)
                before_dedup = len(master_df)
                master_df = master_df.drop_duplicates(subset=['공고번호', '업체명'], keep='last')
                if before_dedup > len(master_df):
                    self.log_message(f"중복 제거: {before_dedup - len(master_df)}건")
            else:
                master_df = new_df
        elif self.existing_df is not None:
            self.log_message("신규 데이터 없음 - 기존 데이터 기반으로 재정리")
            master_df = self.existing_df.copy()

        # ✅ 지역제한 필터링 (기본: 전국만)
        if '지역제한' in master_df.columns:
            original_region_count = len(master_df)
            allowed_mask = master_df['지역제한'].apply(self.is_allowed_region)
            master_df = master_df[allowed_mask].copy()
            removed_region_count = original_region_count - len(master_df)
            self.log_message(f"지역제한 필터링({self.region_scope}): {removed_region_count}건 제거")

        # ✅ 제외 업체 정리: 효림 데이터 삭제
        if '업체명' in master_df.columns:
            original_retired_count = len(master_df)
            retired_mask = (
                master_df['업체명'].astype(str).str.contains('효림', na=False)
            )
            master_df = master_df[~retired_mask].copy()
            removed_retired_count = original_retired_count - len(master_df)
            self.log_message(f"제외업체 필터링(효림): {removed_retired_count}건 제거")

        # ✅ 기초대비사정률 필터링 (99~101% 범위만)
        original_count = len(master_df)
        if '기초대비사정률' in master_df.columns:
            master_df = master_df[
                (master_df['기초대비사정률'] >= 99.0) &
                (master_df['기초대비사정률'] <= 101.0)
            ].copy()
            filtered_count = original_count - len(master_df)
            self.log_message(f"기초대비사정률 필터링: {filtered_count}건 제거 (99~101% 범위 외)")

        # 전체 통합 데이터 저장
        master_file = self.output_dir / "전체_통합_데이터.xlsx"
        master_df.to_excel(master_file, index=False, engine='openpyxl')

        self.log_message(f"전체 통합 데이터 저장 완료: {master_file}")
        self.log_message(f"총 공고 수: {len(all_data)}")
        self.log_message(f"총 데이터 수: {len(master_df)}")

        # 발주처 투찰율별 데이터 분리
        if '발주처투찰률' in master_df.columns:
            self.log_message("\n발주처 투찰율별 데이터 분리 시작...")

            # 고유한 투찰율 찾기
            unique_rates = master_df['발주처투찰률'].dropna().unique()
            unique_rates = sorted([r for r in unique_rates if pd.notna(r)])

            self.log_message(f"발견된 투찰율: {len(unique_rates)}개")

            # 각 투찰율별로 데이터 분리 및 저장
            for rate in unique_rates:
                rate_df = master_df[master_df['발주처투찰률'] == rate].copy()

                if len(rate_df) > 0:
                    # 파일명 생성
                    rate_str = f"{rate:.3f}".replace('.', '_')
                    rate_file = self.output_dir / f"투찰률_{rate_str}%_데이터.xlsx"

                    # 저장
                    rate_df.to_excel(rate_file, index=False, engine='openpyxl')

                    self.log_message(f"  ✓ {rate}% - {len(rate_df)}건 저장: {rate_file.name}")

                self.log_message("발주처 투찰율별 데이터 분리 완료")

            # 요약 통계
            if '미달사유' in master_df.columns:
                미달률 = (master_df['미달사유'].notna().sum() / len(master_df)) * 100
                self.log_message(f"전체 미달률: {미달률:.2f}%")

            if '업체명' in master_df.columns:
                unique_companies = master_df['업체명'].nunique()
                self.log_message(f"참여 업체 수: {unique_companies}개")

    def __del__(self):
        """소멸자 - 로그 파일 닫기"""
        if hasattr(self, 'log_file'):
            self.log_file.close()

def main():
    """메인 함수"""
    import argparse

    parser = argparse.ArgumentParser(description='나라장터 복수예가입찰 데이터 전처리')
    parser.add_argument('--source-dir', default=str(DEFAULT_SOURCE_DIR), help='원본 데이터 디렉토리')
    parser.add_argument('--output-dir', default=str(DEFAULT_OUTPUT_DIR), help='출력 디렉토리')
    parser.add_argument('--single', help='단일 공고번호 처리 (예: 2023-21331)')
    parser.add_argument('--force', action='store_true', help='기존 데이터 무시하고 전체 재처리')
    parser.add_argument('--region-scope', choices=['core3', 'nationwide', 'all'], default='core3',
                       help='지역제한 범위 (기본 core3: 전국/대구/경북, all: 전체)')

    args = parser.parse_args()

    # 전처리기 초기화
    preprocessor = BiddingPreprocessor(
        args.source_dir,
        args.output_dir,
        force=args.force,
        region_scope=args.region_scope
    )

    if args.single:
        # 단일 파일 처리
        main_file = Path(args.source_dir) / f"{args.single}.xlsx"
        participant_file = Path(args.source_dir) / f"{args.single}_참여업체목록.xlsx"

        if not main_file.exists():
            print(f"❌ 메인 파일 없음: {main_file}")
            return

        if not participant_file.exists():
            print(f"❌ 참여업체목록 파일 없음: {participant_file}")
            return

        result_df = preprocessor.merge_bidding_files(main_file, participant_file)
        if result_df is not None:
            output_file = Path(args.output_dir) / f"{args.single}_통합.xlsx"
            result_df.to_excel(output_file, index=False, engine='openpyxl')
            print(f"✓ 처리 완료: {output_file}")
        else:
            print("✗ 처리 실패")
    else:
        # 전체 파일 처리
        preprocessor.process_all_files()

    print("\n전처리 완료!")

if __name__ == "__main__":
    main()
