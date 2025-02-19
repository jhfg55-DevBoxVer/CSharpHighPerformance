namespace SafeManualMemoryManagement
{
    using System;
    //A custom attribute named SMMM has been defined.
    [AttributeUsage(AttributeTargets.Method| AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    //AttributeTargets.Method| AttributeTargets.Class: Specify that this attribute can be used for methods and class.
    //Inherited = false:Specify that this attribute will not be inherited. That is to say, if this attribute is applied to a method in the base class, the methods in the derived class will not automatically inherit this attribute.
    //AllowMultiple = false: Specify that this attribute can only be applied once to a method or class.
    sealed class SMMMAttribute : Attribute//sealed 关键字用于防止类被继承。对于特性类，这通常意味着这个特性类是最终的，并且不能被其他类继承或扩展。
    {
        public SMMMAttribute() { }
    }

}
