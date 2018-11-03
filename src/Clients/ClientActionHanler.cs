﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using static BeetleX.FastHttpApi.RouteTemplateMatch;

namespace BeetleX.FastHttpApi.Clients
{
    class ClientActionHanler
    {

        private List<ClientActionParameter> mRouteParameters = new List<ClientActionParameter>();

        private List<ClientActionParameter> mHeaderParameters = new List<ClientActionParameter>();

        private List<ClientActionParameter> mQueryStringParameters = new List<ClientActionParameter>();

        private List<ClientActionParameter> mDataParameters = new List<ClientActionParameter>();

        public string Method { get; set; }

        public string Name { get; set; }

        public string BaseUrl { get; set; }

        private Dictionary<string, string> mHeaders = new Dictionary<string, string>();

        private Dictionary<string, string> mQueryString = new Dictionary<string, string>();

        public ControllerAttribute Controller { get; set; }

        public IClientBodyFormater Formater { get; set; }

        public MethodInfo MethodInfo { get; set; }

        public RouteTemplateMatch RouteTemplateMatch { get; set; }

        public Type DeclaringType
        { get; set; }

        public ClientActionHanler(MethodInfo method)
        {
            MethodInfo = method;
            Method = "GET";
            Name = method.Name;
            DeclaringType = method.DeclaringType;
            foreach (CHeaderAttribute h in DeclaringType.GetCustomAttributes<CHeaderAttribute>())
            {
                if (!string.IsNullOrEmpty(h.Name) && !string.IsNullOrEmpty(h.Value))
                {
                    mHeaders[h.Name] = h.Value;
                }
            }

            foreach (CHeaderAttribute h in method.GetCustomAttributes<CHeaderAttribute>())
            {
                if (!string.IsNullOrEmpty(h.Name) && !string.IsNullOrEmpty(h.Value))
                {
                    mHeaders[h.Name] = h.Value;
                }
            }

            foreach (CQueryAttribute q in DeclaringType.GetCustomAttributes<CQueryAttribute>())
            {
                if (!string.IsNullOrEmpty(q.Name) && !string.IsNullOrEmpty(q.Value))
                {
                    mQueryString[q.Name] = q.Value;
                }
            }

            foreach (CQueryAttribute q in method.GetCustomAttributes<CQueryAttribute>())
            {
                if (!string.IsNullOrEmpty(q.Name) && !string.IsNullOrEmpty(q.Value))
                {
                    mQueryString[q.Name] = q.Value;
                }
            }

            Formater = method.GetCustomAttribute<FormaterAttribute>();
            if (Formater == null)
                Formater = DeclaringType.GetCustomAttribute<FormaterAttribute>();
            var get = method.GetCustomAttribute<GetAttribute>();
            if (get != null)
            {
                Method = Request.GET;
                if (!string.IsNullOrEmpty(get.Route))
                    RouteTemplateMatch = new RouteTemplateMatch(get.Route);
            }
            var post = method.GetCustomAttribute<PostAttribute>();
            if (post != null)
            {
                Method = Request.POST;
                if (!string.IsNullOrEmpty(post.Route))
                    RouteTemplateMatch = new RouteTemplateMatch(post.Route);
            }
            var del = method.GetCustomAttribute<DelAttribute>();
            if (del != null)
            {
                Method = Request.DELETE;
                if (!string.IsNullOrEmpty(del.Route))
                    RouteTemplateMatch = new RouteTemplateMatch(del.Route);
            }
            var put = method.GetCustomAttribute<PutAttribute>();
            if (put != null)
            {
                Method = Request.PUT;
                if (!string.IsNullOrEmpty(put.Route))
                    RouteTemplateMatch = new RouteTemplateMatch(put.Route);
            }
            Controller = this.DeclaringType.GetCustomAttribute<ControllerAttribute>();
            if (Controller != null)
            {
                if (!string.IsNullOrEmpty(Controller.BaseUrl))
                    BaseUrl = Controller.BaseUrl;
            }
            if (string.IsNullOrEmpty(BaseUrl))
                BaseUrl = "/";
            if (BaseUrl[0] != '/')
                BaseUrl = "/" + BaseUrl;
            if (BaseUrl.Substring(BaseUrl.Length - 1, 1) != "/")
                BaseUrl += "/";
            int index = 0;
            foreach (var p in method.GetParameters())
            {
                ClientActionParameter cap = new ClientActionParameter();
                cap.Name = p.Name;
                cap.ParameterType = p.ParameterType;
                cap.Index = index;
                index++;
                CHeaderAttribute cHeader = p.GetCustomAttribute<CHeaderAttribute>();
                if (cHeader != null)
                {
                    if (!string.IsNullOrEmpty(cHeader.Name))
                        cap.Name = cHeader.Name;
                    mHeaderParameters.Add(cap);
                }
                else
                {
                    CQueryAttribute cQuery = p.GetCustomAttribute<CQueryAttribute>();
                    if (cQuery != null)
                    {
                        if (!string.IsNullOrEmpty(cQuery.Name))
                            cap.Name = cQuery.Name;
                        mQueryStringParameters.Add(cap);
                    }
                    else
                    {
                        if (RouteTemplateMatch != null && RouteTemplateMatch.Items.Find(i => i.Name == p.Name) != null)
                        {
                            mRouteParameters.Add(cap);
                        }
                        else
                        {
                            mDataParameters.Add(cap);
                        }
                    }
                }
            }
        }

        public RequestInfo GetRequest(object[] parameters)
        {
            RequestInfo result = new RequestInfo();
            if (mHeaders.Count > 0)
            {
                if (result.Header == null)
                    result.Header = new Dictionary<string, string>();
                foreach (var kv in mHeaders)
                    result.Header[kv.Key] = kv.Value;
            }
            if (mQueryString.Count > 0)
            {
                if (result.QueryString == null)
                    result.QueryString = new Dictionary<string, string>();
                foreach (var kv in mQueryString)
                    result.QueryString[kv.Key] = kv.Value;
            }
            result.Method = this.Method;
            StringBuilder sb = new StringBuilder();
            sb.Append(BaseUrl);
            if (RouteTemplateMatch != null)
            {
                if (RouteTemplateMatch.Items.Count > 0)
                {
                    List<MatchItem> items = RouteTemplateMatch.Items;
                    for (int i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        if (!string.IsNullOrEmpty(item.Start))
                            sb.Append(item.Start);
                        ClientActionParameter cap = mRouteParameters.Find(p => p.Name == item.Name);
                        if (cap != null)
                            sb.Append(parameters[cap.Index]);

                        if (!string.IsNullOrEmpty(item.Eof))
                            sb.Append(item.Eof);
                    }
                }
                else
                {
                    sb.Append(RouteTemplateMatch.Template);
                }
            }
            else
            {
                sb.Append(MethodInfo.Name);
            }
            if (mDataParameters.Count > 0)
            {
                if (Method == Request.DELETE || Method == Request.GET)
                {
                    if (result.QueryString == null)
                        result.QueryString = new Dictionary<string, string>();
                    foreach (var item in mDataParameters)
                    {
                        result.QueryString[item.Name] = parameters[item.Index].ToString();
                    }
                }
                else
                {
                    var data = new Dictionary<string, object>();
                    foreach (var item in mDataParameters)
                    {
                        data[item.Name] = parameters[item.Index];
                    }
                    result.Data = data;
                }
            }
            if (mHeaderParameters.Count > 0)
            {
                if (result.Header == null)
                    result.Header = new Dictionary<string, string>();
                foreach (var item in mDataParameters)
                {
                    result.Header[item.Name] = (string)parameters[item.Index];
                }
            }
            if (mQueryStringParameters.Count > 0)
            {
                if (result.QueryString == null)
                    result.QueryString = new Dictionary<string, string>();
                foreach (var item in mQueryStringParameters)
                {
                    result.QueryString[item.Name] = (string)parameters[item.Index];
                }
            }
            if (this.MethodInfo.ReturnType != typeof(void))
            {
                result.Type = MethodInfo.ReturnType;
            }
            result.Url = sb.ToString();
            return result;
        }

        public struct RequestInfo
        {
            public string Method;

            public Type Type;

            public string Url;

            public Dictionary<string, object> Data;

            public Dictionary<string, string> Header;

            public Dictionary<string, string> QueryString;
        }

    }

    class ClientActionParameter
    {

        public string Name { get; set; }

        public Type ParameterType { get; set; }

        public int Index { get; set; }

    }
}
