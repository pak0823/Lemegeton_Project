#!/bin/bash
# 유니티 프로젝트 전용 추출 스크립트
gitingest . --exclude-pattern "*.meta,Library/,Temp/,Logs/,UserSettings/,*.unity,*.asset,*.prefab,*.mat,*.anim,*.controller"