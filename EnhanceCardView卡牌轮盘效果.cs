

// 本脚本 实现了 英雄选择/卡牌选择时的轮盘效果：点击置中、鼠标拖拽


using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#region 范例
public class ResManager
{
    public static GameObject InstantiateModel(string str)
    {
        UnityEngine.Object tmp = Resources.Load(str);
        GameObject cell = GameObject.Instantiate(tmp) as GameObject;
        return cell;
    }
}

public class TestCard : CardItem<int, TestCard, TestView>
{
    protected override void UpdateView()
    {
        transform.Find("Text1").GetComponent<Text>().text = (this.DataIndex).ToString();
        transform.Find("Text2").GetComponent<Text>().text = (this.DataIndex).ToString();

    }
}

public class TestView : CardView<int, TestCard, TestView>
{

}

public class Main : MonoBehaviour
{
    public TestView cardView;

    void Start()
    {
        List<int> intList = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            intList.Add(i);
        }

        cardView = TestView.Create(transform, "cell", 7);
        cardView.SetData(intList);

    }


    void Update()
    {
        if (Input.GetKey(KeyCode.A))
        {
            List<int> intList = new List<int>();
            for (int i = 0; i < 10; i++)
            {
                intList.Add(i);
            }
            cardView.SetData(intList);
        }
    }
}
#endregion


/// <summary>
/// 卡牌布局管理器基类，使用时需创建一个子类
/// </summary>
/// <typeparam name="T">数据</typeparam>
/// <typeparam name="C">卡牌</typeparam>
/// <typeparam name="V">布局管理器</typeparam>
public class CardView<T, C, V> : MonoBehaviour where V : CardView<T, C, V> where C : CardItem<T, C, V>
{
    //现在显示几张牌，应当等于Min(maxCardNum, data.Count)
    private int curCardNum;
    //最多显示几张牌，由UI策划确定
    private int maxCardNum;
    //将卡牌的虚拟x坐标转化为位置曲线自变量的系数
    public float positionCoefX;
    public float positionCoefY;
    //将卡牌的虚拟x坐标转化为放缩曲线自变量的系数
    public float scaleCoef;
    //将卡牌的虚拟x坐标转化为深度曲线自变量的系数
    public float depthCoef;
    //动画时长，单位：秒
    private const float tweenTime = 0.2f;
    //显示的卡牌
    private List<C> cardList;
    //卡牌缓存
    private Queue<C> cardCache;
    //m层数据
    private List<T> data;
    //卡牌预制体路径
    private string prefab;
    //卡牌管理器所在的节点、也是卡牌的根节点
    private Transform content;
    //卡牌的pivot的活动范围为[-width，width]，width必须小于BGwidth，不然会出界
    public float width;
    //content的rect的width的一半
    public float BGwidth;
    //最左卡牌的数据的index
    private int from;
    //当前聚焦的卡牌
    public C focusCard;
    //当前卡牌数的一半，例如：5张牌，halfRange应该是2
    public int halfRange;
    //是否正在播放滚动动画，update一直在观察
    private bool isScrolling = false;


    /// <summary>
    /// 创建并初始化
    /// </summary>
    /// <param name="content">卡牌管理器所在的节点、也是卡牌的根节点</param>
    /// <param name="prefab">卡牌预制体路径</param>
    /// <param name="maxCardNum">最多显示几张卡</param>
    /// <returns></returns>
    public static V Create(Transform content, string prefab, int maxCardNum = 7)
    {
        V cardView = content.gameObject.AddComponent<V>();
        cardView.curCardNum = 0;
        cardView.cardList = new List<C>(maxCardNum);
        cardView.cardCache = new Queue<C>();
        cardView.maxCardNum = maxCardNum;
        cardView.prefab = prefab;
        cardView.content = content;
        cardView.BGwidth = (content as RectTransform).rect.width * 0.5f;
        cardView.width = (content as RectTransform).rect.width * 0.4f;
        cardView.positionCoefX = Mathf.PI / 2 / (maxCardNum / 2);
        cardView.positionCoefY = 0;
        cardView.scaleCoef = Mathf.PI / 4 / (maxCardNum / 2);
        cardView.depthCoef = Mathf.PI / 2 / (maxCardNum / 2);
        return cardView;
    }

    public T GetData(int index)
    {
        return data[index];
    }

    //管理器启动的入口：传入数据
    public void SetData(List<T> data)
    {
        this.data = data;
        RefreshView();
    }

    public void RefreshView()
    {
        PrepareCard();
        InitCard();
    }


    //准备卡牌
    public void PrepareCard()
    {
        int expectedCardNum = Mathf.Min(maxCardNum, data.Count);
        //如果现有的卡牌数不够
        if (curCardNum < expectedCardNum)
        {
            for (int i = curCardNum; i < expectedCardNum; i++)
            {
                if (cardCache.Count > 0)//缓存池没空，就从缓存池里取
                {
                    cardList.Add(cardCache.Dequeue());
                }
                else//缓存池空了，就创建新的
                {
                    C cardItem = CardItem<T, C, V>.Create(prefab, this);
                    cardList.Add(cardItem);
                    cardItem.transform.SetParent(content);
                }
            }
        }
        else//现有的卡牌数大于需要展示的卡牌数
        {
            for (int i = curCardNum - 1; i >= expectedCardNum; i--)
            {
                cardCache.Enqueue(cardList[i]);
                cardList.RemoveAt(i);
            }
        }
        curCardNum = expectedCardNum;
        halfRange = curCardNum / 2;
    }


    public void InitCard()
    {
        //准备数据：计算最左边的卡牌显示的数据的index
        from = (data.Count - (curCardNum - 1) / 2);
        //记录一下中心卡牌
        focusCard = cardList[halfRange];
        //为每一张卡牌初始化
        for (int i = 0; i < curCardNum; i++)
        {
            //虚拟坐标初始化：例如五张卡牌的初始虚拟坐标应该为-2,-1,0,1,2
            //注意“虚拟坐标”这个概念，本算法里，每张卡都有一个虚拟坐标，时时刻刻记录它的虚拟位置
            //虚拟坐标是自变量，而卡牌的实际位置、大小、深度都是因变量，两者之间的函数关系由位置曲线、放缩曲线、深度曲线三个函数确定。
            cardList[i].VirtualCoor = -halfRange + i;
            //m层数据初始化：将data赋值给对应的卡牌
            cardList[i].SetData((from + i) % data.Count);
            //位置和大小初始化：将虚拟坐标代入位置曲线、放缩曲线，得到位置和大小
            cardList[i].SetTrans();
            //深度初始化：将虚拟坐标代入深度曲线，得到深度
            cardList[i].SetDepth();
        }
        //将卡牌按照深度重新排序。注意：从此以后cardList的index既不代表数据顺序，也不代表位置顺序，仅仅代表深度顺序，排名越低深度越浅
        cardList.Sort(SortDepth);
        //遮挡关系初始化
        for (int i = 0; i < curCardNum; i++)
        {
            cardList[i].transform.SetSiblingIndex(i);
        }
    }

    private int SortDepth(C a, C b) { return a.Depth.CompareTo(b.Depth); }


    /// <summary>
    /// 立即滚动
    /// </summary>
    /// <param name="displace">位移。卡牌向左位移为负方向</param>
    public void InstantScroll(float displace)
    {
        //是否发生了越界：即某一侧的卡牌越界后瞬移到了另一侧
        bool recycleHappened = false;
        //对每一张卡，更新他的虚拟坐标
        for (int i = 0; i < curCardNum; i++)
        {
            //新的虚拟坐标=旧的虚拟坐标+位移，还要考虑越界问题
            float newCoor = cardList[i].VirtualCoor + displace;
            //向左滚动越界，例如五张卡，初始虚拟坐标是-2,-1,0,1,2，虚拟坐标的活动范围是[-2.5,2.5），小于-2.5就发生了越界
            if (newCoor < -0.5 - halfRange)
            {
                recycleHappened = true;
                //加上一个卡牌周期，把这张卡瞬移到右边（未必是最右边，一帧之内可能出现多张卡牌同时越界）
                newCoor += curCardNum;
                //一张卡，如果发生了越界，那么他的m层需要更新，变为原来的数据index+一个卡牌周期
                cardList[i].SetData((cardList[i].DataIndex + curCardNum) % data.Count);
            }
            //向右滚动越界
            if (newCoor >= 0.5 + halfRange)
            {
                recycleHappened = true;
                newCoor -= curCardNum;
                cardList[i].SetData((cardList[i].DataIndex - curCardNum + data.Count) % data.Count);
            }
            cardList[i].VirtualCoor = newCoor;
            cardList[i].SetTrans();
        }
        //至少一张卡发生了越界<=>原来的中心卡离开了[-0.5,0.5)<=>中心卡换人了<=>需要重新计算遮挡关系
        if (recycleHappened)
        {
            //重新计算遮挡关系
            for (int i = 0; i < curCardNum; i++)
            {
                cardList[i].SetDepth();
            }
            cardList.Sort(SortDepth);
            for (int i = 0; i < curCardNum; i++)
            {
                cardList[i].transform.SetSiblingIndex(i);
            }
            //中心卡换人了
            focusCard = cardList[curCardNum - 1];
        }

    }
    //动画计时器
    private float timeCount;
    //虚拟坐标滚动速度
    private float scrollSpeed;

    public void TweenScroll(float displace)
    {
        timeCount = 0;
        //速度=位移/时间
        scrollSpeed = displace / tweenTime;
        isScrolling = true;
    }

    private void Update()
    {
        if (isScrolling)
        {
            timeCount += Time.deltaTime;
            //如果动画实际播放时间>预计播放时间，那么这最后一帧的位移要特殊计算
            if (timeCount >= tweenTime)
            {
                isScrolling = false;
                InstantScroll((Time.deltaTime - timeCount + tweenTime) * scrollSpeed);
            }
            else//动画正常播放：delta位移=delta时间*速度
            {
                InstantScroll(Time.deltaTime * scrollSpeed);
            }
        }
    }
}



/// <summary>
/// 卡牌基类，使用时需创建子类，通过override UpdateView()方法来刷新v层
/// </summary>
/// <typeparam name="T">数据</typeparam>
/// <typeparam name="C">卡牌</typeparam>
/// <typeparam name="V">布局管理器</typeparam>
public class CardItem<T, C, V> : MonoBehaviour, IPointerClickHandler, IDragHandler, IEndDragHandler where C : CardItem<T, C, V> where V : CardView<T, C, V>
{
    /// <summary>
    /// 创建并初始化
    /// </summary>
    /// <param name="prefab">卡牌预制体路径</param>
    /// <param name="cardView">卡牌布局管理器</param>
    /// <returns></returns>
    public static C Create(string prefab, CardView<T, C, V> cardView)
    {
        GameObject go = ResManager.InstantiateModel(prefab);
        if (ReferenceEquals(go, null)) return null;
        C cardItem = go.AddComponent<C>();
        cardItem.cardView = cardView;
        return cardItem;
    }
    //卡牌布局管理器
    protected CardView<T, C, V> cardView;
    protected T data;

    //虚拟坐标。区别于真实坐标，虚拟坐标是位置曲线的x值，真实坐标是y值，通过修改曲线实现item的紧密程度可配置化
    //例如五张卡的虚拟坐标分别是-2，-1, 0, 1 , 2
    public float VirtualCoor { get; set; }
    //序号。即“this.data”在cardView的“dataList”里的序号
    public int DataIndex { get; set; }

    //深度，用于计算遮挡
    public float Depth { get; set; }

    //接收m层数据，注意传入的参数是data的index而非data本身
    public void SetData(int dataIndex)
    {
        this.data = cardView.GetData(dataIndex);
        this.DataIndex = dataIndex;
        UpdateView();
    }
    //设置位置和大小
    public void SetTrans()
    {
        //位置曲线为sin，从-pi/2到pi/2，这样中间稀疏两边密集
        (transform as RectTransform).anchoredPosition = new Vector2(Mathf.Sin(VirtualCoor * cardView.positionCoefX) * cardView.width, 0);
        //放缩曲线为cos，从-pi/4到pi/4，这样中间大两边小
        transform.localScale = Vector3.one * Mathf.Cos(VirtualCoor * cardView.scaleCoef);

    }
    //设置深度
    public void SetDepth()
    {
        //深度曲线为cos，从-pi/2到pi/2，这样中间大两边小
        this.Depth = Mathf.Cos(VirtualCoor * cardView.depthCoef);
    }

    //子类通过实现这个方法来刷新v层
    protected virtual void UpdateView() { }


    public void OnPointerClick(PointerEventData eventData)
    {
        cardView.TweenScroll(-Mathf.RoundToInt(VirtualCoor));
    }

    //拖拽时
    public void OnDrag(PointerEventData eventData)
    {
        //计算当前的位置：屏幕坐标转anchor坐标
        float curPosiX = eventData.position.x - cardView.BGwidth;
        //上一帧位置
        float lastPosiX = curPosiX - eventData.delta.x;
        //通过真实位置计算虚拟坐标，计算方式是一个分段函数：在[-cardView.width,cardView.width]区间是A*acrsin(B*x)，其他地方是y=C*x
        //在[-cardView.width,cardView.width]区间里拖拽时，鼠标和卡牌几乎没有相对位移（实际上因为放缩的关系还是会有点）
        float curVirtualCoor;
        float lastVirtualCoor;
        if (curPosiX > cardView.width || curPosiX < -cardView.width)
        {
            curVirtualCoor = curPosiX / cardView.width * cardView.halfRange;
        }
        else
        {
            curVirtualCoor = Mathf.Asin(curPosiX / cardView.width) / cardView.positionCoefX;
        }
        if (lastPosiX > cardView.width || lastPosiX < -cardView.width)
        {
            lastVirtualCoor = lastPosiX / cardView.width * cardView.halfRange;
        }
        else
        {
            lastVirtualCoor = Mathf.Asin(lastPosiX / cardView.width) / cardView.positionCoefX;
        }
        //这一帧的虚拟坐标 - 上一帧虚拟坐标 = 位移
        cardView.InstantScroll(curVirtualCoor - lastVirtualCoor);
    }

    //拖拽结束时，中心卡虚拟坐标归零
    public void OnEndDrag(PointerEventData eventData)
    {
        cardView.TweenScroll(-cardView.focusCard.VirtualCoor);
    }


}

