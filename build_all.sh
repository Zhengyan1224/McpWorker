#!/bin/bash

# 获取要传递给子脚本的参数
runtime="${1:-linux-x64}"
framework="${2:-net8.0}"
output_dir="${3:-../../publish}"

# 获取当前目录下的所有子目录
subdirs=$(find . -maxdepth 1 -type d ! -name ".")

for dir in $subdirs; do
    # 进入子目录
    cd "$dir" || continue  # 如果目录不存在或其他问题，跳过
    
    echo "Processing directory: $dir"
    
    # 检查是否存在build.sh
    if [ -f "build.sh" ]; then
        echo "Found build.sh in $dir, executing..."
        # 将参数传递给子脚本
        ./build.sh "$runtime" "$framework" "$output_dir"
        echo "Finished executing build.sh in $dir"
    else
        echo "No build.sh found in $dir"
    fi
    
    # 返回上一级目录
    cd ..
done

echo "All subdirectories processed."