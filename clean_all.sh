#!/bin/bash

# 获取当前目录下的所有子目录
subdirs=$(find . -maxdepth 1 -type d ! -name ".")

for dir in $subdirs; do
    # 进入子目录
    cd "$dir" || continue  # 如果目录不存在或其他问题，跳过
    
    echo "Processing directory: $dir"
    
    # 检查是否存在bin和obj目录
    if [ -d "bin" ] && [ -d "obj" ]; then
        echo "Found bin and obj directories in $dir, deleting..."
        rm -rf bin obj logs
        echo "Successfully deleted bin and obj directories in $dir"
    else
        echo "No bin and obj directories found in $dir"
    fi
    
    # 返回上一级目录
    cd ..
done

echo "All subdirectories processed."