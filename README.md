# HMXStudio
## 代码拉取
```shell
git clone http://gitlab.hymson.com.cn:3080/AIVISION/hmxstudio.git
```
## 代码结构
### hmxstudio  
-- |   
&nbsp;&nbsp; 01Main 框架代码  
-- | -- |  
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; VM.Start 框架入口  
--|  
&nbsp;&nbsp; 02Plugins 界面组件  
-- | -- |  
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; 000常用工具   
-- | -- |      
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; 001图像处理   
-- | -- |  
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; 002检测识别   
-- | -- |    
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; 003几何测量  
-- | -- |  
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; 004几何关系     
-- | -- |  
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; 005坐标标定   
-- | -- |  
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; 006对位工具   
-- | -- |  
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; 007逻辑工具   
-- | -- |  
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; 008系统工具   
-- | -- |  
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; 009变量工具   
-- | -- |  
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; 010文件通讯   
-- | -- |  
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; 011仪器仪表   
-- | -- |  
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; 012深度学习     
-- | -- |  
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; 013测试工具     
-- | -- |  
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; 014激光工具  
--|  
&nbsp;&nbsp; 04Camera 相机工具  
--|  
&nbsp;&nbsp; 00Demo 示例代码  
--|   
&nbsp;&nbsp; 00Exe 运行需要的依赖以及动态生成的文件  
--|   
&nbsp;&nbsp; 07Dll 运行需要的依赖  
--|   
&nbsp;&nbsp; packages 依赖库  
--|   
&nbsp;&nbsp; .gitignore git忽略文件  
--|   
&nbsp;&nbsp; README.md 说明文档

# git 提交规范
- feat 新功能
- fix 修补 bug
- docs 文档
- style 格式、样式(不影响代码运行的变动)
- refactor 重构(即不是新增功能，也不是修改 BUG 的代码)
- perf 优化相关，比如提升性能、体验
- test 添加测试
- build 编译相关的修改，对项目构建或者依赖的改动
- ci 持续集成修改
- chore 构建过程或辅助工具的变动
- revert 回滚到上一个版本
- workflow 工作流改进
- mod 不确定分类的修改
- wip 开发中
- types 类型
## 示例 
### 标准提交规范
```shell
git add .
```
```shell
git commit -m 'feat: 新增功能'
```
```shell
git push
```
### 切换分支(切换到master分支)
```shell
git checkout master
```
### 合并分支(将dev分支合并到当前分支)
```shell
git merge dev
```
### 查看分支
```shell
git branch
```
### 删除分支
```shell
git branch -d dev
```
### 强行推送本地代码（慎用）
```shell
git push origin master -f
```
### 放弃本地分支全部使用远程分支
1. 获取远程仓库最新数据
```shell
git fetch --all
```
2. 将分支重置为远程某个分支代码(这里master为对应的分支名)
```shell
git reset --hard origin/master 
```
3. 将远程仓库的代码拉取到本地
```shell
git pull
```

