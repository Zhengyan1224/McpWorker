using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Zhengyan.Commons.Web.Mvc;

/// <summary>
/// 全局路由前缀配置
/// </summary>
public class RouteConvention : IApplicationModelConvention
{
    /// <summary>
    /// 定义一个路由前缀变量
    /// </summary>
    private readonly AttributeRouteModel _centralPrefix;

    /// <summary>
    /// 调用时传入指定的路由前缀
    /// </summary>
    /// <param name="routeTemplateProvider"></param>
    public RouteConvention(IRouteTemplateProvider routeTemplateProvider)
    {
        _centralPrefix = new AttributeRouteModel(routeTemplateProvider);
    }

    // 接口的 Apply 方法
    public void Apply(ApplicationModel application)
    {
        // 遍历所有的 Controller
        foreach (var controller in application.Controllers)
        {
            // 处理控制器级别的路由
            ProcessControllerRoutes(controller);

            // 处理方法级别的路由
            ProcessActionRoutes(controller);
        }
    }

    /// <summary>
    /// 处理控制器级别的路由
    /// </summary>
    /// <param name="controller"></param>
    private void ProcessControllerRoutes(ControllerModel controller)
    {
        // 已经标记了 RouteAttribute 的 Controller
        var matchedSelectors = controller.Selectors.Where(x => x.AttributeRouteModel != null).ToList();
        if (matchedSelectors.Any())
        {
            foreach (var selectorModel in matchedSelectors)
            {
                // 在当前路由上再添加一个路由前缀
                selectorModel.AttributeRouteModel = AttributeRouteModel.CombineAttributeRouteModel(_centralPrefix, selectorModel.AttributeRouteModel);
            }
        }

        // 没有标记 RouteAttribute 的 Controller
        var unmatchedSelectors = controller.Selectors.Where(x => x.AttributeRouteModel == null).ToList();
        if (unmatchedSelectors.Any())
        {
            foreach (var selectorModel in unmatchedSelectors)
            {
                // 添加一个路由前缀
                selectorModel.AttributeRouteModel = _centralPrefix;
            }
        }
    }

    /// <summary>
    /// 处理方法级别的路由
    /// </summary>
    /// <param name="controller"></param>
    private void ProcessActionRoutes(ControllerModel controller)
    {
        // 遍历控制器中的每个方法
        foreach (var action in controller.Actions)
        {
            // 已经标记了 RouteAttribute 的方法
            var matchedSelectors = action.Selectors.Where(x => x.AttributeRouteModel != null).ToList();
            if (matchedSelectors.Any())
            {
                foreach (var selectorModel in matchedSelectors)
                {
                    // 在当前路由上再添加一个路由前缀
                    selectorModel.AttributeRouteModel = AttributeRouteModel.CombineAttributeRouteModel(_centralPrefix, selectorModel.AttributeRouteModel);
                }
            }

            // 没有标记 RouteAttribute 的方法
            var unmatchedSelectors = action.Selectors.Where(x => x.AttributeRouteModel == null).ToList();
            if (unmatchedSelectors.Any())
            {
                foreach (var selectorModel in unmatchedSelectors)
                {
                    // 添加一个路由前缀
                    selectorModel.AttributeRouteModel = _centralPrefix;
                }
            }
        }
    }
}