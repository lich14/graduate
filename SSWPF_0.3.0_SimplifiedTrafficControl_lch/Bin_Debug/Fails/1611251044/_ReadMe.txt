963 和 965 逆向死锁，磁钉号在 2899 和 2642

问题场景为：

963 约 2642，367

965 约 2899，360

看360。运算前，963前面没有965，965前面也是。



360 磁钉状况：

TP ID : 2642;	Resv AGV : 964;	Intersected
RouteAGVs (Passed): 	963	964	965	967


TP ID : 2899;	Resv AGV : 965;	Intersected
RouteAGVs (Passed): 	955	(960)	963	(964)	965	967	968


367 磁钉状况：

TP ID : 2642;	Resv AGV : 963;	Intersected
RouteAGVs (Passed): 	963	(964)	965	967

TP ID : 2899;	Resv AGV : 965;	Intersected
RouteAGVs (Passed): 	955	(960)	963	(964)	965	967	968


367 的强连通分量检测输入有问题。应该是 IsUnsurpassable 的计算问题，AGVRoute.CurrClaimLength 的记录方式有误。

